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
        private readonly BufferBlock<PendingInsert> _insertionQueue;
        private readonly Task[] _workerTasks;
        private readonly SemaphoreSlim _queriesInFlightSemaphore;
        private bool _stopped;

        private class PendingInsert
        {
            public IStatement Statement { get; set; }
            public TaskCompletionSource<RowSet> Completion { get; set; }
        }

        public ParallelPersistor(ISession session, int asyncWorkersCount, int maximumInFlightStatements)
        {
            _session = session;
            _workerTasks = new Task[asyncWorkersCount];
            _queriesInFlightSemaphore = new SemaphoreSlim(maximumInFlightStatements);
            _insertionQueue = new BufferBlock<PendingInsert>();
        }
        
        public Task Insert(IStatement statement)
        {
            var taskCompletionSource = new TaskCompletionSource<RowSet>();
            _insertionQueue.Post(new PendingInsert { Statement = statement, Completion = taskCompletionSource });
            return taskCompletionSource.Task;
        }

        public void Start()
        {
            for (var i = 0; i < _workerTasks.Length; ++i)
            {
                _workerTasks[i] = Task.Factory.StartNew(async () =>
                {
                    while(true)
                    {
                        PendingInsert pendingInsert = null;
                        try
                        {
                            pendingInsert = await _insertionQueue.ReceiveAsync();
                            await _queriesInFlightSemaphore.WaitAsync();
                            var rowSet = await _session.ExecuteAsync(pendingInsert.Statement);

                            pendingInsert.Completion.SetResult(rowSet);
                        }
                        catch (InvalidOperationException) // thrown by BufferBlock when stopping
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            _log.Error(ex);
                            pendingInsert?.Completion.SetException(ex);
                        }
                        finally
                        {
                            _queriesInFlightSemaphore.Release();
                        }
                    }
                }, TaskCreationOptions.LongRunning).Unwrap();
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
    }
}