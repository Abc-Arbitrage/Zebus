using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Abc.Zebus.Monitoring;
using Abc.Zebus.Scan;
using Abc.Zebus.Scan.Pipes;
using Abc.Zebus.Util.Collections;
using Abc.Zebus.Util.Extensions;
using log4net;

namespace Abc.Zebus.Dispatch
{
    public class MessageDispatcher : IMessageDispatcher, IProvideQueueLength
    {
        private static readonly ConcurrentList<IMessageHandlerInvoker> _emptyInvokers = new ConcurrentList<IMessageHandlerInvoker>();
        private readonly IMessageHandlerInvokerLoader[] _invokerLoaders;
        private ConcurrentDictionary<MessageTypeId, ConcurrentList<IMessageHandlerInvoker>> _invokers = new ConcurrentDictionary<MessageTypeId, ConcurrentList<IMessageHandlerInvoker>>();
        private readonly ILog _logger = LogManager.GetLogger(typeof(MessageDispatcher));
        private readonly IPipeManager _pipeManager;
        private readonly ConcurrentDictionary<string, DispatcherTaskScheduler> _schedulersByQueueName = new ConcurrentDictionary<string, DispatcherTaskScheduler>(StringComparer.OrdinalIgnoreCase);
        private readonly IDispatcherTaskSchedulerFactory _taskSchedulerFactory;
        private Func<Assembly, bool> _assemblyFilter;
        private Func<Type, bool> _handlerFilter;
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

        public void ConfigureHandlerFilter(Func<Type, bool> handlerFiler)
        {
            _handlerFilter = handlerFiler;
        }

        public void LoadMessageHandlerInvokers()
        {
            _invokers = new ConcurrentDictionary<MessageTypeId, ConcurrentList<IMessageHandlerInvoker>>();
            var typeSource = CreateTypeSource();

            foreach (var invokerLoader in _invokerLoaders)
            {
                var loadedInvokers = invokerLoader.LoadMessageHandlerInvokers(typeSource);
                foreach (var invoker in loadedInvokers)
                {
                    if (_handlerFilter != null && !_handlerFilter(invoker.MessageHandlerType))
                        continue;

                    var messageTypeInvokers = _invokers.GetOrAdd(new MessageTypeId(invoker.MessageType), x => new ConcurrentList<IMessageHandlerInvoker>());
                    messageTypeInvokers.Add(invoker);
                }
            }
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
            if (!_isRunning)
                throw new InvalidOperationException("MessageDispatcher is stopped");

            var invokers = _invokers.GetValueOrDefault(dispatch.Message.TypeId(), _emptyInvokers)
                                    .Where(dispatch.ShouldInvoke)
                                    .ToList();

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
            var messageTypeInvokers = _invokers.GetOrAdd(eventHandlerInvoker.MessageTypeId, x => new ConcurrentList<IMessageHandlerInvoker>());
            messageTypeInvokers.Add(eventHandlerInvoker);
        }

        public void RemoveInvoker(IMessageHandlerInvoker eventHandlerInvoker)
        {
            ConcurrentList<IMessageHandlerInvoker> messageTypeInvokers;
            if (!_invokers.TryGetValue(eventHandlerInvoker.MessageTypeId, out messageTypeInvokers))
                return;

            messageTypeInvokers.Remove(eventHandlerInvoker);
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

        public int PurgeQueues()
        {
            return _schedulersByQueueName.Values.Sum(taskScheduler => taskScheduler.PurgeTasks());
        }

        public int GetReceiveQueueLength()
        {
            return _schedulersByQueueName.Values.Sum(taskScheduler => taskScheduler.TaskCount);
        }
    }
}