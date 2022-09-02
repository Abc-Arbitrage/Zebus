using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Abc.Zebus.Monitoring;
using Abc.Zebus.Scan;
using Abc.Zebus.Util.Extensions;
using Microsoft.Extensions.Logging;

namespace Abc.Zebus.Dispatch
{
    public class MessageDispatcher : IMessageDispatcher, IProvideQueueLength
    {
        private static readonly List<IMessageHandlerInvoker> _emptyInvokers = new();
        private static readonly ILogger _logger = ZebusLogManager.GetLogger(typeof(MessageDispatcher));

        private readonly ConcurrentDictionary<string, DispatchQueue> _dispatchQueues = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();
        private readonly IMessageHandlerInvokerLoader[] _invokerLoaders;
        private readonly IDispatchQueueFactory _dispatchQueueFactory;
        private ConcurrentDictionary<MessageTypeId, List<IMessageHandlerInvoker>> _invokers = new();
        private Func<Assembly, bool>? _assemblyFilter;
        private Func<Type, bool>? _handlerFilter;
        private Func<Type, bool>? _messageFilter;
        private Status _status;

        public MessageDispatcher(IMessageHandlerInvokerLoader[] invokerLoaders, IDispatchQueueFactory dispatchQueueFactory)
        {
            _invokerLoaders = invokerLoaders;
            _dispatchQueueFactory = dispatchQueueFactory;
        }

        public event Action? Starting;
        public event Action? Stopping;

        public void ConfigureAssemblyFilter(Func<Assembly, bool> assemblyFilter) => _assemblyFilter = assemblyFilter;

        public void ConfigureHandlerFilter(Func<Type, bool> handlerFilter) => _handlerFilter = handlerFilter;

        public void ConfigureMessageFilter(Func<Type, bool> messageFilter) => _messageFilter = messageFilter;

        public event Action? MessageHandlerInvokersUpdated;

        public void LoadMessageHandlerInvokers()
        {
            _status = Status.Loaded;

            _invokers = LoadInvokers();

            LoadDispatchQueues();

            MessageHandlerInvokersUpdated?.Invoke();
        }

        private ConcurrentDictionary<MessageTypeId, List<IMessageHandlerInvoker>> LoadInvokers()
        {
            var invokers = new ConcurrentDictionary<MessageTypeId, List<IMessageHandlerInvoker>>();
            var typeSource = CreateTypeSource();

            foreach (var invokerLoader in _invokerLoaders)
            {
                var loadedInvokers = invokerLoader.LoadMessageHandlerInvokers(typeSource);

                foreach (var invoker in loadedInvokers)
                {
                    if (_handlerFilter != null && !_handlerFilter(invoker.MessageHandlerType))
                        continue;

                    if (_messageFilter != null && !_messageFilter(invoker.MessageType))
                        continue;

                    var messageTypeInvokers = invokers.GetOrAdd(new MessageTypeId(invoker.MessageType), x => new List<IMessageHandlerInvoker>());
                    messageTypeInvokers.Add(invoker);
                }
            }

            return invokers;
        }

        private void LoadDispatchQueues()
        {
            foreach (var invoker in _invokers.Values.SelectMany(x => x).Where(x => !_dispatchQueues.ContainsKey(x.DispatchQueueName)))
            {
                _dispatchQueues.TryAdd(invoker.DispatchQueueName, _dispatchQueueFactory.Create(invoker.DispatchQueueName));
            }
        }

        public IEnumerable<MessageTypeId> GetHandledMessageTypes() => _invokers.Keys;

        public IEnumerable<IMessageHandlerInvoker> GetMessageHandlerInvokers() => _invokers.SelectMany(x => x.Value);

        public void Dispatch(MessageDispatch dispatch)
        {
            var invokers = _invokers.GetValueOrDefault(dispatch.Message.TypeId(), _emptyInvokers);
            Dispatch(dispatch, invokers);
        }

        public void Dispatch(MessageDispatch dispatch, Func<Type, bool> handlerFilter)
        {
            var invokers = _invokers.GetValueOrDefault(dispatch.Message.TypeId(), _emptyInvokers).Where(x => handlerFilter(x.MessageHandlerType)).ToList();
            Dispatch(dispatch, invokers);
        }

        private void Dispatch(MessageDispatch dispatch, List<IMessageHandlerInvoker> invokers)
        {
            switch (_status)
            {
                case Status.Stopped:
                    throw new InvalidOperationException("MessageDispatcher is stopped");

                case Status.Stopping:
                    if (dispatch.IsLocal)
                        break;

                    throw new InvalidOperationException("MessageDispatcher is stopping");
            }

            if (invokers.Count == 0)
            {
                dispatch.SetIgnored();
                return;
            }

            dispatch.SetHandlerCount(invokers.Count);

            foreach (var invoker in invokers)
            {
                if (invoker.ShouldHandle(dispatch.Message))
                    Dispatch(dispatch, invoker);
            }
        }

        public void Stop()
        {
            if (_status != Status.Loaded && _status != Status.Started)
                return;

            _status = Status.Stopping;

            OnStopping();

            WaitUntilAllMessagesAreProcessed();

            _status = Status.Stopped;

            var stopTasks = _dispatchQueues.Values.Select(dispatchQueue => Task.Run(dispatchQueue.Stop)).ToArray();
            Task.WaitAll(stopTasks);
        }

        private void OnStopping()
        {
            try
            {
                Stopping?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stopping event handler error");
            }
        }

        public void Start()
        {
            if (_status == Status.Started)
                return;

            if (_status != Status.Loaded)
                throw new InvalidOperationException("MessageDispatcher should be loaded before start");

            OnStarting();

            lock (_lock)
            {
                _status = Status.Started;

                foreach (var dispatchQueue in _dispatchQueues.Values)
                {
                    dispatchQueue.Start();
                }
            }
        }

        private void OnStarting()
        {
            try
            {
                Starting?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Starting event handler error");
            }
        }

        private void WaitUntilAllMessagesAreProcessed()
        {
            bool continueWait;
            do
            {
                continueWait = false;

                foreach (var dispatchQueue in _dispatchQueues.Values)
                {
                    continueWait = dispatchQueue.WaitUntilAllMessagesAreProcessed() || continueWait;
                }
            } while (continueWait);
        }

        public void AddInvoker(IMessageHandlerInvoker newEventHandlerInvoker)
        {
            lock (_lock)
            {
                var existingMessageTypeInvokers = _invokers.GetValueOrDefault(newEventHandlerInvoker.MessageTypeId) ?? new List<IMessageHandlerInvoker>();
                var newMessageTypeInvokers = new List<IMessageHandlerInvoker>(existingMessageTypeInvokers.Count + 1);
                newMessageTypeInvokers.AddRange(existingMessageTypeInvokers);
                newMessageTypeInvokers.Add(newEventHandlerInvoker);

                var dispatchQueueName = newEventHandlerInvoker.DispatchQueueName;
                if (!_dispatchQueues.ContainsKey(dispatchQueueName))
                {
                    var dispatchQueue = _dispatchQueueFactory.Create(dispatchQueueName);
                    if (_dispatchQueues.TryAdd(dispatchQueueName, dispatchQueue) && _status == Status.Started)
                        dispatchQueue.Start();
                }

                _invokers[newEventHandlerInvoker.MessageTypeId] = newMessageTypeInvokers;
            }

            MessageHandlerInvokersUpdated?.Invoke();
        }

        public void RemoveInvoker(IMessageHandlerInvoker eventHandlerInvoker)
        {
            lock (_lock)
            {
                var messageTypeInvokers = _invokers.GetValueOrDefault(eventHandlerInvoker.MessageTypeId);
                if (messageTypeInvokers == null)
                    return;

                var newMessageTypeInvokers = new List<IMessageHandlerInvoker>(messageTypeInvokers.Where(x => x != eventHandlerInvoker));
                _invokers[eventHandlerInvoker.MessageTypeId] = newMessageTypeInvokers;
            }

            MessageHandlerInvokersUpdated?.Invoke();
        }

        public int Purge() => _dispatchQueues.Values.Sum(x => x.Purge());

        public int GetReceiveQueueLength() => _dispatchQueues.Values.Sum(x => x.QueueLength);

        private TypeSource CreateTypeSource()
        {
            var typeSource = new TypeSource();

            if (_assemblyFilter != null)
                typeSource.AssemblyFilter = _assemblyFilter;

            if (_handlerFilter != null)
                typeSource.TypeFilter = _handlerFilter;

            return typeSource;
        }

        private void Dispatch(MessageDispatch dispatch, IMessageHandlerInvoker invoker)
        {
            var dispatchQueue = _dispatchQueues[invoker.DispatchQueueName];
            dispatchQueue.RunOrEnqueue(dispatch, invoker);
        }

        private enum Status
        {
            Stopped,
            Loaded,
            Started,
            Stopping,
        }
    }
}
