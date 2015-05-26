using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Dispatch.Pipes;
using Abc.Zebus.Monitoring;
using Abc.Zebus.Scan;
using Abc.Zebus.Util.Extensions;
using log4net;

namespace Abc.Zebus.Dispatch
{
    public class MessageDispatcher : IMessageDispatcher, IProvideQueueLength
    {
        private static readonly List<IMessageHandlerInvoker> _emptyInvokers = new List<IMessageHandlerInvoker>();
        private readonly IMessageHandlerInvokerLoader[] _invokerLoaders;
        private readonly ILog _logger = LogManager.GetLogger(typeof(MessageDispatcher));
        private readonly IPipeManager _pipeManager;
        private readonly ConcurrentDictionary<string, DispatcherTaskScheduler> _schedulersByQueueName = new ConcurrentDictionary<string, DispatcherTaskScheduler>(StringComparer.OrdinalIgnoreCase);
        private readonly IDispatcherTaskSchedulerFactory _taskSchedulerFactory;
        private ConcurrentDictionary<MessageTypeId, List<IMessageHandlerInvoker>> _invokers = new ConcurrentDictionary<MessageTypeId, List<IMessageHandlerInvoker>>();
        private Func<Assembly, bool> _assemblyFilter;
        private Func<Type, bool> _handlerFilter;
        private Func<Type, bool> _messageFilter;
        private volatile bool _isRunning;
        
        public MessageDispatcher(IPipeManager pipeManager, IMessageHandlerInvokerLoader[] invokerLoaders, IDispatcherTaskSchedulerFactory taskSchedulerFactory)
        {
            _pipeManager = pipeManager;
            _invokerLoaders = invokerLoaders;
            _taskSchedulerFactory = taskSchedulerFactory;
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
            if (!_isRunning)
                throw new InvalidOperationException("MessageDispatcher is stopped");

            if (invokers.Count == 0)
            {
                dispatch.SetIgnored();
                return;
            }

            dispatch.SetHandlerCount(invokers.Count);

            foreach (var invoker in invokers)
            {
                Dispatch(dispatch, invoker);
            }
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;

            var stopTasks = _schedulersByQueueName.Values.Select(scheduler => Task.Factory.StartNew(scheduler.Stop)).ToArray();
            Task.WaitAll(stopTasks);
        }

        public void Start()
        {
            if (_isRunning)
                return;

            _isRunning = true;

            foreach (var dispatcherTaskScheduler in _schedulersByQueueName.Values)
                dispatcherTaskScheduler.Start();
        }

        public void AddInvoker(IMessageHandlerInvoker eventHandlerInvoker)
        {
            lock (_invokers)
            {
                var messageTypeInvokers = _invokers.GetValueOrDefault(eventHandlerInvoker.MessageTypeId) ?? new List<IMessageHandlerInvoker>();
                var newMessageTypeInvokers = new List<IMessageHandlerInvoker>(messageTypeInvokers.Count + 1);
                newMessageTypeInvokers.AddRange(messageTypeInvokers);
                newMessageTypeInvokers.AddRange(eventHandlerInvoker);

                _invokers[eventHandlerInvoker.MessageTypeId] = newMessageTypeInvokers;
            }
            
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
        }

        public int Purge()
        {
            return _schedulersByQueueName.Values.Sum(taskScheduler => taskScheduler.PurgeTasks());
        }

        public int GetReceiveQueueLength()
        {
            return _schedulersByQueueName.Values.Sum(taskScheduler => taskScheduler.TaskCount);
        }

        private DispatcherTaskScheduler CreateAndStartTaskScheduler(string queueName)
        {
            var taskScheduler = _taskSchedulerFactory.Create(queueName);
            taskScheduler.Start();

            return taskScheduler;
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
            var context = dispatch.Context.WithDispatchQueueName(invoker.DispatchQueueName);
            var invocation = _pipeManager.BuildPipeInvocation(invoker, dispatch.Message, context);

            var isInSameDispatchQueue = ShouldRunInCurrentDispatchQueue(invoker.DispatchQueueName, dispatch.Context.DispatchQueueName);

            if (invoker.CanInvokeSynchronously && (dispatch.ShouldRunSynchronously || isInSameDispatchQueue))
                DispatchSync(invocation, dispatch);
            else
                DispatchAsync(invocation, dispatch);
        }

        private void DispatchAsync(PipeInvocation invocation, MessageDispatch dispatch)
        {
            var invocationTask = invocation.RunAsync();
            invocationTask.ContinueWith(task => dispatch.SetHandled(invocation.Invoker, GetException(task)), TaskContinuationOptions.ExecuteSynchronously);

            if (invocationTask.Status != TaskStatus.Created)
                return;

            if (invocation.Invoker.ShouldCreateStartedTasks)
            {
                var exception = new InvalidProgramException(string.Format("{0}.Handle({1}) did not start the returned task", invocation.Invoker.MessageHandlerType.Name, invocation.Invoker.MessageType.Name));
                dispatch.SetHandled(invocation.Invoker, exception);
                return;
            }

            var taskScheduler = GetTaskScheduler(invocation.Invoker.DispatchQueueName);
            invocationTask.Start(taskScheduler);
        }

        private Exception GetException(Task task)
        {
            if (!task.IsFaulted)
                return null;

            var exception = task.Exception != null ? task.Exception.InnerException : new Exception("Task failed");
            _logger.Error(exception);

            return exception;
        }

        private TaskScheduler GetTaskScheduler(string queueName)
        {
            return _schedulersByQueueName.GetOrAdd(queueName, CreateAndStartTaskScheduler);
        }

        private static void DispatchSync(PipeInvocation invocation, MessageDispatch dispatch)
        {
            Exception exception = null;
            try
            {
                invocation.Run();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            dispatch.SetHandled(invocation.Invoker, exception);
        }

        private static bool ShouldRunInCurrentDispatchQueue(string invokerDispatchQueueName, string currentDispatchQueueName)
        {
            return invokerDispatchQueueName != null && invokerDispatchQueueName.Equals(currentDispatchQueueName, StringComparison.OrdinalIgnoreCase);
        }
    }
}