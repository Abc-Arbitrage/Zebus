using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Abc.Zebus.Persistence.Util;
using Cassandra;
using log4net;

namespace Abc.Zebus.Persistence.CQL.Util
{
    public class ParallelPersistor : IDisposable
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(ParallelPersistor));

        private readonly ISession _session;
        private readonly Action<Exception> _errorReportingAction;
        private readonly BufferBlock<PendingInsert> _insertionQueue;
        private readonly Task[] _workerTasks;
        private readonly SemaphoreSlim _queriesWaitingInLineSemaphore;
        private readonly int _maximumQueueSize;
        private bool _stopped;

        public ParallelPersistor(ISession session, int asyncWorkersCount, Action<Exception> errorReportingAction = null)
        {
            _session = session;
            _errorReportingAction = errorReportingAction;
            _workerTasks = new Task[asyncWorkersCount];
            _maximumQueueSize = asyncWorkersCount * 100;
            _queriesWaitingInLineSemaphore = new SemaphoreSlim(_maximumQueueSize);
            _insertionQueue = new BufferBlock<PendingInsert>();
        }

        public int QueueSize => _maximumQueueSize - _queriesWaitingInLineSemaphore.CurrentCount;

        public int WorkersCount => _workerTasks.Length;

        public Task Insert(IStatement statement)
        {
            _queriesWaitingInLineSemaphore.Wait();
            var taskCompletionSource = new TaskCompletionSource<RowSet>();
            _insertionQueue.Post(new PendingInsert { Statement = statement, Completion = taskCompletionSource });

            return taskCompletionSource.Task;
        }

        public void Start()
        {
            for (var i = 0; i < _workerTasks.Length; ++i)
            {
                _workerTasks[i] = WorkerLoopAsync();
            }
        }

        private async Task WorkerLoopAsync()
        {
            while (true)
            {
                PendingInsert pendingInsert = null;
                try
                {
                    pendingInsert = await _insertionQueue.ReceiveAsync().ConfigureAwait(false);
                    var rowSet = await _session.ExecuteAsync(pendingInsert.Statement).ConfigureAwait(false);

                    pendingInsert.Completion.SetResult(rowSet);
                }
                catch (InvalidOperationException) // thrown by BufferBlock when stopping
                {
                    _log.Info("Received stop signal");
                    break;
                }
                catch (Exception ex)
                {
                    _log.Error(ex);
                    pendingInsert?.Completion.SetException(ex);
                    _errorReportingAction?.Invoke(ex);
                }
                finally
                {
                    _queriesWaitingInLineSemaphore.Release();
                }
            }
        }

        public void Stop()
        {
            _log.Info($"Stopping {nameof(ParallelPersistor)}");

            WaitForQueueToBeEmpty();

            _insertionQueue.Complete();
            _insertionQueue.Completion.Wait();
            Task.WaitAll(_workerTasks);

            _log.Info($"{nameof(ParallelPersistor)} stopped");
        }

        private void WaitForQueueToBeEmpty()
        {
            Task.Run(() =>
            {
                while (_insertionQueue.Count > 0)
                {
                    Thread.Sleep(10.Milliseconds());
                }
            }).Wait(10.Seconds());
        }

        public void Dispose()
        {
            if (_stopped)
                return;

            _stopped = true;
            Stop();
        }

        private class PendingInsert
        {
            public IStatement Statement { get; set; }
            public TaskCompletionSource<RowSet> Completion { get; set; }
        }
    }
}