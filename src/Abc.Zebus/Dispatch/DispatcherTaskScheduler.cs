using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Util.Collections;
using log4net;

namespace Abc.Zebus.Dispatch
{
    public class DispatcherTaskScheduler : TaskScheduler, IDisposable
    {
        private readonly string _threadName;
        private FlushableBlockingCollection<Task> _tasks = new FlushableBlockingCollection<Task>();
//        private BlockingCollection<Task> _tasks = new BlockingCollection<Task>();
        private Thread _thread;
        private volatile bool _isRunning;

        private readonly ILog _logger = LogManager.GetLogger(typeof(DispatcherTaskScheduler));

        public int TaskCount
        {
            get { return _tasks.Count; }
        }

        public override int MaximumConcurrencyLevel
        {
            get { return 1; }
        }

        public bool IsRunning
        {
            get { return _isRunning; }
        }

        internal DispatcherTaskScheduler()
        {
        }

        public DispatcherTaskScheduler(string threadName = null)
        {
            _threadName = threadName;
        }

        public void Dispose()
        {
            Stop();
            if (_tasks != null)
            {
                _tasks.Dispose();
                _tasks = null;
            }
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            throw new NotSupportedException("The FlushableBlockingCollection doesn't allow peeking");
        }


        protected override void QueueTask(Task task)
        {
            _tasks.Add(task);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (taskWasPreviouslyQueued)
                TryDequeue(task);

            return TryExecuteTask(task);
        }

        private void CreateAndStartThread()
        {
            _thread = new Thread(ThreadProc) { IsBackground = true };
            if (_threadName != null)
                _thread.Name = _threadName;

            _thread.Start();
        }

        private void ThreadProc()
        {
            foreach (var t in _tasks.GetConsumingEnumerable())
            {
                if (!IsRunning)
                    break;

                TryExecuteTask(t);
            }

            _logger.Info(_threadName + " processing stopped");
        }

        public void Stop()
        {
            if (!IsRunning)
                return;

            _isRunning = false;
            
            if(_tasks.Count == 0)
                _tasks.Add(new Task(() => {}));
            
            _thread.Join();
        }

        public void ClearTasks()
        {
            if(IsRunning)
                throw new InvalidOperationException("Tasks can be cleared only when the TaskScheduler is stopped");

            _tasks = new FlushableBlockingCollection<Task>();
//            _tasks = new BlockingCollection<Task>();
        }

        public virtual int PurgeTasks()
        {
            var flushedTasks = _tasks.Flush(true);
            return flushedTasks.Count;
//            return 0;
        }

        public void Start()
        {
            if (IsRunning)
                return;

            _isRunning = true;
            CreateAndStartThread();
        }
    }
}