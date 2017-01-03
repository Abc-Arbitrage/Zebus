using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly int _batchSize;
        private readonly string _name;
        private FlushableBlockingCollection<Entry> _queue = new FlushableBlockingCollection<Entry>();
        private Thread _thread;
        private volatile bool _isRunning;

        public DispatchQueue(IPipeManager pipeManager, int batchSize, string name)
        {
            _pipeManager = pipeManager;
            _batchSize = batchSize;
            _name = name;
        }

        public bool IsRunning => _isRunning;

        public int QueueLength => _queue.Count;

        public void Dispose()
        {
            Stop();
        }

        public void Enqueue(MessageDispatch dispatch, IMessageHandlerInvoker invoker)
        {
            _queue.Add(new Entry(dispatch, invoker));
        }

        public void Start()
        {
            if (_isRunning)
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
            if (!_isRunning)
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
                _logger.Info(_name + " processing started");

                var batch = new Batch(_batchSize);

                foreach (var entries in _queue.GetConsumingEnumerable(_batchSize).TakeWhile(x => _isRunning))
                {
                    ProcessEntries(entries, batch);
                }

                _logger.Info(_name + " processing stopped");
            }
            finally
            {
                _currentDispatchQueueName = null;
            }
        }

        private void ProcessEntries(List<Entry> entries, Batch batch)
        {
            batch.Add(entries.First());

            foreach (var entry in entries.Skip(1))
            {
                if (!entry.Invoker.CanMergeWith(batch.FirstEntry.Invoker))
                    RunBatch(batch);

                batch.Add(entry);
            }

            RunBatch(batch);
        }

        private void RunBatch(Batch batch)
        {
            if (!_isRunning)
            {
                batch.Clear();
                return;
            }
            var exception = Run(batch.FirstEntry.Invoker, batch.FirstEntry.Dispatch.Context, batch.Messages);
            batch.SetHandled(exception);
            batch.Clear();
        }

        public void Run(MessageDispatch dispatch, IMessageHandlerInvoker invoker)
        {
            if (!_isRunning)
                return;

            var exception = Run(invoker, dispatch.Context, new List<IMessage> { dispatch.Message });
            dispatch.SetHandled(invoker, exception);
        }

        private Exception Run(IMessageHandlerInvoker invoker, MessageContext context, List<IMessage> messages)
        {
            try
            {
                var invocation = _pipeManager.BuildPipeInvocation(invoker, messages, context);
                invocation.Run();

                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
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

        // for unit tests
        internal static string GetCurrentDispatchQueueName()
        {
            return _currentDispatchQueueName;
        }

        // for unit tests
        internal static IDisposable SetCurrentDispatchQueueName(string queueName)
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

        private class Batch
        {
            public readonly List<Entry> Entries;
            public readonly List<IMessage> Messages;

            public Batch(int capacity)
            {
                Entries = new List<Entry>(capacity);
                Messages = new List<IMessage>(capacity);
            }

            public Entry FirstEntry => Entries[0];

            public void Add(Entry entry)
            {
                Entries.Add(entry);
                Messages.Add(entry.Dispatch.Message);
            }

            public void SetHandled(Exception error)
            {
                foreach (var entry in Entries)
                {
                    entry.Dispatch.SetHandled(entry.Invoker, error);
                }
            }

            public void Clear()
            {
                Entries.Clear();
                Messages.Clear();
            }
        }
    }
}