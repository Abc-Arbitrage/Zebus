using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Abc.Zebus.Lotus;
using Abc.Zebus.Monitoring;
using Abc.Zebus.Persistence.Storage;
using Abc.Zebus.Persistence.Util;
using log4net;

namespace Abc.Zebus.Persistence.Matching
{
    public class InMemoryMessageMatcher : IInMemoryMessageMatcher, IProvideQueueLength
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(InMemoryMessageMatcher));
        private readonly BlockingCollection<MatcherEntry> _persistenceQueue = new BlockingCollection<MatcherEntry>();
        private readonly ConcurrentSet<MessageKey> _acksMessageKeys = new ConcurrentSet<MessageKey>();
        private readonly IPersistenceConfiguration _persistenceConfiguration;
        private readonly IStorage _storage;
        private readonly IBus _bus;
        private readonly Thread _workerThread;

        public InMemoryMessageMatcher(IPersistenceConfiguration persistenceConfiguration, IStorage storage, IBus bus)
        {
            _persistenceConfiguration = persistenceConfiguration;
            _storage = storage;
            _bus = bus;
            _workerThread = new Thread(ThreadProc) { Name = "InMemoryMessageMatcher.ThreadProc" };
        }

        public long CassandraInsertCount { get; private set; }
        public long InMemoryAckCount { get; private set; }

        public void Start()
        {
            using (var signal = new ManualResetEventSlim())
            {
                _workerThread.Start(signal);
                signal.Wait();
            }
            _logger.Info("InMemoryMessageMatcher started");
        }

        private void ThreadProc(object state)
        {
            var signal = (ManualResetEventSlim)state;
            signal.Set();

            var delay = _persistenceConfiguration.PersisterDelay;
            var batch = new List<MatcherEntry>();

            foreach (var entry in _persistenceQueue.GetConsumingEnumerable())
            {
                if (entry.IsEventWaitHandle)
                {
                    entry.WaitHandle.Set();
                    continue;
                }

                WaitForDelay(entry, delay);

                PairUpOrAddToBatch(batch, entry);
                LoadAndPersistBatch(batch);
            }
        }

        private static void WaitForDelay(MatcherEntry entry, TimeSpan? delay)
        {
            if (delay == null)
                return;

            var timestampUtc = SystemDateTime.UtcNow;
            while (timestampUtc - entry.TimestampUtc < delay.Value)
            {
                Thread.Sleep(100);
                timestampUtc = SystemDateTime.UtcNow;
            }
        }

        private void PairUpOrAddToBatch(List<MatcherEntry> batch, MatcherEntry entry)
        {
            if (entry.IsAck)
            {
                var isAlreadyPairedUp = !_acksMessageKeys.Remove(new MessageKey(entry.PeerId, entry.MessageId));
                if (isAlreadyPairedUp)
                    return;
            }
            else
            {
                var isPairUpSuccessfull = _acksMessageKeys.Remove(new MessageKey(entry.PeerId, entry.MessageId));
                if (isPairUpSuccessfull)
                {
                    InMemoryAckCount++;
                    return;
                }
            }

            batch.Add(entry);
        }

        private void LoadAndPersistBatch(List<MatcherEntry> batch)
        {
            try
            {
                LoadBatch(batch);
                PersistBatch(batch);

                batch.Clear();
            }
            catch (StorageTimeoutException ex)
            {
                _bus.Publish(new CustomProcessingFailed(GetType().FullName, ex.ToString(), SystemDateTime.UtcNow));
            }
            catch (Exception ex)
            {
                _logger.Error("Unexpected error happened, trying to persist messages one by one");
                _bus.Publish(new CustomProcessingFailed(GetType().FullName, ex.ToString(), SystemDateTime.UtcNow));
                PersistMessagesOneByOne(batch);
                batch.Clear();
            }
        }

        private void LoadBatch(List<MatcherEntry> batch)
        {
            MatcherEntry entry;
            while (batch.Count < _persistenceConfiguration.PersisterBatchSize && _persistenceQueue.TryTake(out entry))
            {
                PairUpOrAddToBatch(batch, entry);
            }
        }

        private void PersistMessagesOneByOne(IEnumerable<MatcherEntry> batch)
        {
            foreach (var entry in batch)
            {
                try
                {
                    PersistBatch(new List<MatcherEntry> { entry });
                }
                catch
                {
                    _logger.Fatal("A message of type [" + entry.MessageTypeName + "] failed to be persisted in one by one mode");
                }
            }
        }

        // Internal for testing purposes
        internal void PersistBatch(List<MatcherEntry> batch)
        {
            var entriesToInsert = batch.Where(x => !x.IsEventWaitHandle).ToList();
            
            _storage.Write(entriesToInsert);
            
            foreach (var entry in batch.Where(x => x.IsEventWaitHandle))
            {
                entry.WaitHandle.Set();
            }

            CassandraInsertCount += entriesToInsert.Count;
        }

        public void Stop()
        {
            if (_persistenceQueue.IsAddingCompleted)
            {
                _logger.InfoFormat("InMemoryMessageMatcher already stopped");
                return;
            }

            _logger.InfoFormat("Stopping InMemoryMessageMatcher, {0} messages on queue to persist", _persistenceQueue.Count);
            _persistenceQueue.CompleteAdding();
            _workerThread.Join();
            _logger.Info("InMemoryMessageMatcher stopped");
        }

        public void EnqueueMessage(PeerId peerId, MessageId messageId, MessageTypeId messageTypeId, byte[] bytes)
        {
            var entry = MatcherEntry.Message(peerId, messageId, messageTypeId, bytes);
            _persistenceQueue.Add(entry);
        }

        public void EnqueueAck(PeerId peerId, MessageId messageId)
        {
            _acksMessageKeys.Add(new MessageKey(peerId, messageId));

            var entry = MatcherEntry.Ack(peerId, messageId);
            _persistenceQueue.Add(entry);
        }

        public void EnqueueWaitHandle(EventWaitHandle waitHandle)
        {
            _persistenceQueue.Add(MatcherEntry.EventWaitHandle(waitHandle));
        }

        public int GetReceiveQueueLength()
        {
            return _persistenceQueue.Count;
        }

        public int Purge()
        {
            var purgedMessageCount = 0;
            while (_persistenceQueue.Count > 0)
            {
                MatcherEntry entry;
                if (_persistenceQueue.TryTake(out entry))
                    ++purgedMessageCount;
            }
            return purgedMessageCount;
        }

        private struct MessageKey : IEquatable<MessageKey>
        {
            private readonly PeerId _peerId;
            private readonly MessageId _messageId;

            public MessageKey(PeerId peerId, MessageId messageId)
            {
                _messageId = messageId;
                _peerId = peerId;
            }

            public bool Equals(MessageKey other)
            {
                return _peerId.Equals(other._peerId) && _messageId.Equals(other._messageId);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (_peerId.GetHashCode() * 397) ^ _messageId.GetHashCode();
                }
            }
        }
    }
}