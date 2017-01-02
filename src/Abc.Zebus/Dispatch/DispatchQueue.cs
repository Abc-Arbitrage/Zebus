using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Dispatch.Pipes;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Collections;
using log4net;

namespace Abc.Zebus.Dispatch
{
    public class DispatchQueue : IDisposable
    {
        [ThreadStatic]
        private static string _currentDispatchQueueName;

        private readonly ILog _logger = LogManager.GetLogger(typeof(DispatchQueue));
        private readonly IPipeManager _pipeManager;
        private readonly string _name;
        private FlushableBlockingCollection<Entry> _queue = new FlushableBlockingCollection<Entry>();
        private Thread _thread;
        private volatile bool _isRunning;

        public DispatchQueue(IPipeManager pipeManager, string name)
        {
            _pipeManager = pipeManager;
            _name = name;
        }

        public bool IsRunning => _isRunning;

        public int QueueLength => _queue.Count;

        public void Dispose()
        {
            Stop();
            if (_queue != null)
            {
                _queue.Dispose();
                _queue = null;
            }
        }

        public void Enqueue(MessageDispatch dispatch, IMessageHandlerInvoker invoker)
        {
            _queue.Add(new Entry(dispatch, invoker));
        }

        public void Start()
        {
            if (IsRunning)
                return;

            _isRunning = true;
            _thread = new Thread(ThreadProc)
            {
                IsBackground = true,
                Name = $"{_name}.DispatchThread",
            };

            _thread.Start();
        }

        public void Stop()
        {
            if (!IsRunning)
                return;

            _isRunning = false;

            _queue.CompleteAdding();
            
            _thread.Join();

            _queue = new FlushableBlockingCollection<Entry>();
        }

        private void ThreadProc()
        {
            _currentDispatchQueueName = _name;
            try
            {
                foreach (var entry in _queue.GetConsumingEnumerable())
                {
                    if (!IsRunning)
                        break;

                    Run(entry.Dispatch, entry.Invoker);
                }

                _logger.Info(_name + " processing stopped");
            }
            finally
            {
                _currentDispatchQueueName = null;
            }
        }

        public void Run(MessageDispatch dispatch, IMessageHandlerInvoker invoker)
        {
            Exception exception = null;
            try
            {
                var invocation = _pipeManager.BuildPipeInvocation(invoker, new List<IMessage> { dispatch.Message }, dispatch.Context);
                invocation.Run();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            dispatch.SetHandled(invoker, exception);
        }

        public void RunAsync(MessageDispatch dispatch, IMessageHandlerInvoker invoker)
        {
            var invocation = _pipeManager.BuildPipeInvocation(invoker, new List<IMessage> { dispatch.Message }, dispatch.Context);

            var invocationTask = invocation.RunAsync();
            invocationTask.ContinueWith(task => dispatch.SetHandled(invocation.Invoker, GetException(task)), TaskContinuationOptions.ExecuteSynchronously);
        }

        private Exception GetException(Task task)
        {
            if (!task.IsFaulted)
                return null;

            var exception = task.Exception != null ? task.Exception.InnerException : new Exception("Task failed");
            _logger.Error(exception);

            return exception;
        }

        public virtual int Purge()
        {
            var flushedEntries = _queue.Flush();
            return flushedEntries.Count;
        }

        public void RunOrEnqueue(MessageDispatch dispatch, IMessageHandlerInvoker invoker)
        {
            if (_currentDispatchQueueName == _name)
                Run(dispatch, invoker);
            else
                Enqueue(dispatch, invoker);
        }

        public static string GetCurrentDispatchQueueName()
        {
            return _currentDispatchQueueName;
        }

        public static IDisposable SetCurrentDispatchQueueName(string queueName)
        {
            _currentDispatchQueueName = queueName;

            return new DisposableAction(() => _currentDispatchQueueName = null);
        }

        private struct Entry
        {
            public Entry(MessageDispatch dispatch, IMessageHandlerInvoker invoker)
            {
                Dispatch = dispatch;
                Invoker = invoker;
            }

            public MessageDispatch Dispatch;
            public IMessageHandlerInvoker Invoker;
        }
    }
}