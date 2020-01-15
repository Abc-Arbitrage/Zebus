using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Monitoring;
using Abc.Zebus.Scan;
using Abc.Zebus.Util.Extensions;

namespace Abc.Zebus.Dispatch
{
    public class MessageDispatcher : IMessageDispatcher, IProvideQueueLength
    {
        private static readonly List<IMessageHandlerInvoker> _emptyInvokers = new List<IMessageHandlerInvoker>();
        private readonly ConcurrentDictionary<string, DispatchQueue> _dispatchQueues = new ConcurrentDictionary<string, DispatchQueue>(StringComparer.OrdinalIgnoreCase);
        private readonly IMessageHandlerInvokerLoader[] _invokerLoaders;
        private readonly IDispatchQueueFactory _dispatchQueueFactory;
        private ConcurrentDictionary<MessageTypeId, List<IMessageHandlerInvoker>> _invokers = new ConcurrentDictionary<MessageTypeId, List<IMessageHandlerInvoker>>();
        private Func<Assembly, bool> _assemblyFilter;
        private Func<Type, bool> _handlerFilter;
        private Func<Type, bool> _messageFilter;
        private MessageDispatcherStatus _status;

        internal MessageDispatcherStatus Status => _status;

        public MessageDispatcher(IMessageHandlerInvokerLoader[] invokerLoaders, IDispatchQueueFactory dispatchQueueFactory)
        {
            _invokerLoaders = invokerLoaders;
            _dispatchQueueFactory = dispatchQueueFactory;
        }

        public void ConfigureAssemblyFilter(Func<Assembly, bool> assemblyFilter)
        {
            _assemblyFilter = assemblyFilter;
        }

        public void ConfigureHandlerFilter(Func<Type, bool> handlerFilter)
        {
            _handlerFilter = handlerFilter;
        }

        public void ConfigureMessageFilter(Func<Type, bool> messageFilter)
        {
            _messageFilter = messageFilter;
        }

        public event Action MessageHandlerInvokersUpdated;

        public void LoadMessageHandlerInvokers()
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

            Thread.MemoryBarrier();
            _invokers = invokers;
            MessageHandlerInvokersUpdated?.Invoke();

        }

        public IEnumerable<MessageTypeId> GetHandledMessageTypes()
        {
            return _invokers.Keys;
        }

        public IEnumerable<IMessageHandlerInvoker> GetMessageHanlerInvokers()
        {
            return _invokers.SelectMany(x => x.Value);
        }

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
                case MessageDispatcherStatus.Stopped:
                    throw new InvalidOperationException("MessageDispatcher is stopped");

                case MessageDispatcherStatus.Stopping:
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
            if (_status != MessageDispatcherStatus.Started)
                return;

            _status = MessageDispatcherStatus.Stopping;

            WaitUntilAllMessagesAreProcessed();

            _status = MessageDispatcherStatus.Stopped;

            var stopTasks = _dispatchQueues.Values.Select(dispatchQueue => Task.Factory.StartNew(dispatchQueue.Stop)).ToArray();
            Task.WaitAll(stopTasks);
        }

        public void Start()
        {
            switch (_status)
            {
                case MessageDispatcherStatus.Started:
                    return;

                case MessageDispatcherStatus.Stopping:
                    throw new InvalidOperationException("MessageDispatcher is stopping");
            }

            _status = MessageDispatcherStatus.Started;

            foreach (var dispatchQueue in _dispatchQueues.Values)
            {
                dispatchQueue.Start();
            }
        }

        private void WaitUntilAllMessagesAreProcessed()
        {
            bool continueWait;

            do
            {
                continueWait = false;

                foreach (var dispatchQueue in _dispatchQueues.Values)
                    continueWait = dispatchQueue.WaitUntilAllMessagesAreProcessed() || continueWait;
            } while (continueWait);
        }

        public void AddInvoker(IMessageHandlerInvoker newEventHandlerInvoker)
        {
            lock (_invokers)
            {
                var existingMessageTypeInvokers = _invokers.GetValueOrDefault(newEventHandlerInvoker.MessageTypeId) ?? new List<IMessageHandlerInvoker>();
                var newMessageTypeInvokers = new List<IMessageHandlerInvoker>(existingMessageTypeInvokers.Count + 1);
                newMessageTypeInvokers.AddRange(existingMessageTypeInvokers);
                newMessageTypeInvokers.Add(newEventHandlerInvoker);

                _invokers[newEventHandlerInvoker.MessageTypeId] = newMessageTypeInvokers;
            }
            MessageHandlerInvokersUpdated?.Invoke();
        }

        public void RemoveInvoker(IMessageHandlerInvoker eventHandlerInvoker)
        {
            lock (_invokers)
            {
                var messageTypeInvokers = _invokers.GetValueOrDefault(eventHandlerInvoker.MessageTypeId);
                if (messageTypeInvokers == null)
                    return;

                var newMessageTypeInvokers = new List<IMessageHandlerInvoker>(messageTypeInvokers.Where(x => x != eventHandlerInvoker));
                _invokers[eventHandlerInvoker.MessageTypeId] = newMessageTypeInvokers;
            }
            MessageHandlerInvokersUpdated?.Invoke();
        }

        public int Purge()
        {
            return _dispatchQueues.Values.Sum(x => x.Purge());
        }

        public int GetReceiveQueueLength()
        {
            return _dispatchQueues.Values.Sum(x => x.QueueLength);
        }

        private DispatchQueue CreateAndStartDispatchQueue(string queueName)
        {
            var dispatchQueue = _dispatchQueueFactory.Create(queueName);
            dispatchQueue.Start();

            return dispatchQueue;
        }

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
            var dispatchQueue = _dispatchQueues.GetOrAdd(invoker.DispatchQueueName, CreateAndStartDispatchQueue);
            dispatchQueue.RunOrEnqueue(dispatch, invoker);
        }
    }

    internal enum MessageDispatcherStatus
    {
        Stopped,
        Stopping,
        Started
    }
}
