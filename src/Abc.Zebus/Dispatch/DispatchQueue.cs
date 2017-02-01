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
        private FlushableBlockingCollection<Entry> _queue = new FlushableBlockingCollection<Entry>();
        private Thread _thread;
        private volatile bool _isRunning;

        public string Name { get; }

        private bool IsCurrentDispatchQueue => _currentDispatchQueueName == Name;

        public DispatchQueue(IPipeManager pipeManager, int batchSize, string name)
        {
            _pipeManager = pipeManager;
            _batchSize = batchSize;
            Name = name;
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

        internal void Enqueue(Action action)
        {
            _queue.Add(new Entry(action));
        }

        public void Start()
        {
            if (_isRunning)
                return;

            _isRunning = true;
            _thread = new Thread(ThreadProc)
            {
                IsBackground = true,
                Name = $"{Name}.DispatchThread",
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
            _currentDispatchQueueName = Name;
            try
            {
                _logger.InfoFormat("{0} processing started", Name);
                SynchronizationContext.SetSynchronizationContext(new DispatchQueueSynchronizationContext(this));

                var batch = new Batch(_batchSize);

                foreach (var entries in _queue.GetConsumingEnumerable(_batchSize).TakeWhile(x => _isRunning))
                {
                    ProcessEntries(entries, batch);
                }

                _logger.InfoFormat("{0} processing stopped", Name);
            }
            finally
            {
                _currentDispatchQueueName = null;
            }
        }

        private void ProcessEntries(List<Entry> entries, Batch batch)
        {
            foreach (var entry in entries)
            {
                if (entry.Action != null)
                {
                    RunBatch(batch);
                    RunAction(entry.Action);
                    continue;
                }

                if (batch.Messages.Count == 0)
                {
                    batch.Add(entry);
                    continue;
                }

                if (!entry.Invoker.CanMergeWith(batch.FirstEntry.Invoker))
                    RunBatch(batch);

                batch.Add(entry);
            }

            RunBatch(batch);
        }

        private void RunAction(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }

        private void RunSingle(MessageDispatch dispatch, IMessageHandlerInvoker invoker)
        {
            if (!_isRunning)
                return;

            var batch = new Batch(1);
            batch.Add(new Entry(dispatch, invoker));

            RunBatch(batch);
        }

        private void RunBatch(Batch batch)
        {
            if (!_isRunning)
            {
                batch.Clear();
                return;
            }

            if (batch.Messages.Count == 0)
                return;

            try
            {
                switch (batch.FirstEntry.Invoker.Mode)
                {
                    case MessageHandlerInvokerMode.Synchronous:
                    {
                        var invocation = _pipeManager.BuildPipeInvocation(batch.FirstEntry.Invoker, batch.Messages, batch.FirstEntry.Dispatch.Context);
                        invocation.Run();
                        batch.SetHandled(null);
                        break;
                    }

                    case MessageHandlerInvokerMode.Asynchronous:
                    {
                        var asyncBatch = batch.Clone();
                        var invocation = _pipeManager.BuildPipeInvocation(asyncBatch.FirstEntry.Invoker, asyncBatch.Messages, asyncBatch.FirstEntry.Dispatch.Context);
                        invocation.RunAsync().ContinueWith(task => asyncBatch.SetHandled(GetException(task)), TaskContinuationOptions.ExecuteSynchronously);
                        break;
                    }

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                batch.SetHandled(ex);
            }
            finally
            {
                batch.Clear();
            }
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
            if (dispatch.ShouldRunSynchronously || IsCurrentDispatchQueue)
                RunSingle(dispatch, invoker);
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
                Action = null;
            }

            public Entry(Action action)
            {
                Dispatch = null;
                Invoker = null;
                Action = action;
            }

            public readonly MessageDispatch Dispatch;
            public readonly IMessageHandlerInvoker Invoker;
            public readonly Action Action;
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
                    entry.Dispatch.SetHandled(entry.Invoker, error);
            }

            public void Clear()
            {
                Entries.Clear();
                Messages.Clear();
            }

            public Batch Clone()
            {
                var clone = new Batch(Math.Max(Entries.Count, Messages.Count));
                clone.Entries.AddRange(Entries);
                clone.Messages.AddRange(Messages);
                return clone;
            }
        }

        private class DispatchQueueSynchronizationContext : SynchronizationContext
        {
            private readonly DispatchQueue _dispatchQueue;

            public DispatchQueueSynchronizationContext(DispatchQueue dispatchQueue)
            {
                _dispatchQueue = dispatchQueue;
            }

            public override void Post(SendOrPostCallback d, object state)
            {
                _dispatchQueue.Enqueue(() => d(state));
            }
        }
    }
}
