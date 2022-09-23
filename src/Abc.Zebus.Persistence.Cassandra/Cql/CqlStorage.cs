using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.Cassandra.Data;
using Abc.Zebus.Persistence.Cassandra.Util;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Persistence.Reporter;
using Abc.Zebus.Persistence.Storage;
using Abc.Zebus.Persistence.Util;
using Cassandra;
using Cassandra.Data.Linq;
using Microsoft.Extensions.Logging;

namespace Abc.Zebus.Persistence.Cassandra.Cql
{
    public class CqlStorage : ICqlStorage, IDisposable
    {
        private const int _maxParallelInsertTasks = 64;
        private static readonly ILogger _log = ZebusLogManager.GetLogger(typeof(CqlStorage));
        private static readonly DateTime _unixOrigin = new(1970, 1, 1, 0, 0, 0, 0);

        private readonly PersistenceCqlDataContext _dataContext;
        private readonly PeerStateRepository _peerStateRepository;
        private readonly IPersistenceConfiguration _configuration;
        private readonly IReporter _reporter;
        private readonly PreparedStatement _preparedStatement;

        public CqlStorage(PersistenceCqlDataContext dataContext, IPersistenceConfiguration configuration, IReporter reporter)
        {
            _dataContext = dataContext;
            _peerStateRepository = new PeerStateRepository(dataContext);
            _configuration = configuration;
            _reporter = reporter;

            _preparedStatement = dataContext.Session.Prepare(dataContext.PersistentMessages.Insert(new PersistentMessage()).SetTTL(0).SetTimestamp(default(DateTimeOffset)).ToString());
        }

        public Task CleanBucketTask { get; private set; } = Task.CompletedTask;

        public Func<DateTime> DateTimeSource
        {
            get => _peerStateRepository.DateTimeSource;
            set => _peerStateRepository.DateTimeSource = value;
        }

        public Dictionary<PeerId, int> GetNonAckedMessageCounts()
        {
            return _peerStateRepository.GetAllKnownPeers()
                                       .ToDictionary(x => x.PeerId, x => x.NonAckedMessageCount);
        }

        public void Start()
        {
            _peerStateRepository.Initialize();
        }

        public void Stop()
        {
            Dispose();
        }

        public Task Write(IList<MatcherEntry> entriesToPersist)
        {
            if (entriesToPersist.Count == 0)
                return Task.CompletedTask;

            _reporter.AddStorageReport(ToStorageReport(entriesToPersist));

            var countByPeer = new Dictionary<PeerId, int>();
            foreach (var matcherEntry in entriesToPersist)
            {
                var shouldInvestigatePeer = _configuration.PeerIdsToInvestigate != null && _configuration.PeerIdsToInvestigate.Contains(matcherEntry.PeerId.ToString());
                if (shouldInvestigatePeer)
                    _log.LogInformation($"Storage requested for peer {matcherEntry.PeerId}, Type: {matcherEntry.Type}, Message Id: {matcherEntry.MessageId}");

                var countDelta = matcherEntry.IsAck ? -1 : 1;
                countByPeer[matcherEntry.PeerId] = countDelta + countByPeer.GetValueOrDefault(matcherEntry.PeerId);

                if (shouldInvestigatePeer)
                    _log.LogInformation($"Count delta computed for peer {matcherEntry.PeerId}, will increment: {countDelta}");
            }

            var insertTasks = new List<Task>();
            var remaining = new SemaphoreSlim(_maxParallelInsertTasks);
            foreach (var matcherEntry in entriesToPersist)
            {
                var gotSlot = remaining.Wait(TimeSpan.FromSeconds(10));
                if (!gotSlot)
                    _log.LogWarning("Could not get slot to insert in cassandra after 10 second.");

                var messageDateTime = matcherEntry.MessageId.GetDateTimeForV2OrV3();
                var rowTimestamp = matcherEntry.IsAck ? messageDateTime.AddTicks(10) : messageDateTime;
                var boundStatement = _preparedStatement.Bind(
                    BucketIdHelper.GetBucketId(messageDateTime),
                    matcherEntry.IsAck,
                    matcherEntry.MessageId.Value,
                    matcherEntry.PeerId.ToString(),
                    matcherEntry.MessageBytes,
                    messageDateTime.Ticks,
                    (int)PeerState.MessagesTimeToLive.TotalSeconds,
                    ToUnixMicroSeconds(rowTimestamp)
                );

                var insertTask = _dataContext.Session.ExecuteAsync(boundStatement);
                insertTasks.Add(insertTask);
                insertTask.ContinueWith(t =>
                                        {
                                            var shouldInvestigatePeer = _configuration.PeerIdsToInvestigate != null && _configuration.PeerIdsToInvestigate.Contains(matcherEntry.PeerId.ToString());
                                            if (shouldInvestigatePeer)
                                                _log.LogInformation($"Storage done for peer {matcherEntry.PeerId}, Type: {matcherEntry.Type}, Message Id: {matcherEntry.MessageId}, TaskResult: {t.Status}");

                                            if (t.IsFaulted)
                                                _log.LogError(t.Exception, "Error while inserting to Cassandra");

                                            remaining.Release();
                                        },
                                        TaskContinuationOptions.ExecuteSynchronously);
            }

            var updateNonAckedCountTasks = new List<Task>();
            foreach (var countForPeer in countByPeer)
            {
                updateNonAckedCountTasks.Add(_peerStateRepository.UpdateNonAckMessageCount(countForPeer.Key, countForPeer.Value));
            }

            return Task.WhenAll(insertTasks.Concat(updateNonAckedCountTasks));
        }

        private static StorageReport ToStorageReport(IList<MatcherEntry> entriesToPersist)
        {
            var fattestMessage = entriesToPersist.OrderByDescending(msg => msg.MessageBytes?.Length ?? 0).First();
            var entriesByTypeName = entriesToPersist.ToLookup(x => x.MessageTypeName)
                                                    .ToDictionary(xs => xs.Key, xs => new MessageTypeStatistics(xs.Count(), xs.Sum(x => x.MessageBytes?.Length) ?? 0));
            return new StorageReport(entriesToPersist.Count, entriesToPersist.Sum(msg => msg.MessageBytes?.Length ?? 0), fattestMessage.MessageBytes?.Length ?? 0, fattestMessage.MessageTypeName, entriesByTypeName);
        }

        public Task RemovePeer(PeerId peerId)
        {
            return _peerStateRepository.RemovePeer(peerId);
        }

        public IMessageReader? CreateMessageReader(PeerId peerId)
        {
            _log.LogInformation($"Creating message reader for peer {peerId}");
            var peerState = _peerStateRepository.GetPeerStateFor(peerId);
            if (peerState == null)
            {
                _log.LogInformation($"PeerState for peer {peerId} does not exist, no reader can be created");
                return null;
            }

            var reader = new CqlMessageReader(_dataContext, peerState);
            _log.LogInformation("CqlMessageReader created");

            return reader;
        }

        public void Dispose()
        {
        }

        public Task UpdateNewOldestMessageTimestamp(PeerState peer)
        {
            var newOldestUnackedTimestampMinusSafetyOffset = ComputeOldestUnackedMessageTimestampInTicks(peer) - PeerState.OldestNonAckedMessageTimestampSafetyOffset.Ticks;
            if (newOldestUnackedTimestampMinusSafetyOffset == peer.OldestNonAckedMessageTimestampInTicks)
                return Task.CompletedTask;

            if (newOldestUnackedTimestampMinusSafetyOffset < peer.OldestNonAckedMessageTimestampInTicks)
            {
                _log.LogWarning($"OldestNonAckedMessageTimestampInTicks moved backward for {peer.PeerId}, Value: {new DateTime(newOldestUnackedTimestampMinusSafetyOffset)}");
            }
            else
            {
                CleanBuckets(peer.PeerId, peer.OldestNonAckedMessageTimestampInTicks, newOldestUnackedTimestampMinusSafetyOffset);
            }

            return _peerStateRepository.UpdateNewOldestMessageTimestamp(peer, newOldestUnackedTimestampMinusSafetyOffset);
        }

        private void CleanBuckets(in PeerId peerId, long oldestUnackedMessageTimestampInTicks, long newOldestMessageTimestamp)
        {
            var firstBucketToDelete = BucketIdHelper.GetBucketId(oldestUnackedMessageTimestampInTicks);
            var lastBucketToDelete = BucketIdHelper.GetPreviousBucketId(newOldestMessageTimestamp);
            if (firstBucketToDelete > lastBucketToDelete)
                return;

            var bucketsToDelete = BucketIdHelper.GetBucketsCollection(firstBucketToDelete, lastBucketToDelete).ToArray();
            var peerIdString = peerId.ToString();

            CleanBucketTask = _dataContext.PersistentMessages
                                          .Where(x => x.PeerId == peerIdString && bucketsToDelete.Contains(x.BucketId))
                                          .Delete()
                                          .ExecuteAsync();
        }

        /// <summary>
        /// public for debug / analysis.
        /// </summary>
        public long ComputeOldestUnackedMessageTimestampInTicks(PeerState peer)
        {
            var peerId = peer.PeerId.ToString();
            var bucketIds = BucketIdHelper.GetBucketsCollection(peer.OldestNonAckedMessageTimestampInTicks);

            var firstUnackedMessageTimestamp = bucketIds.SelectMany(ReadMessages)
                                                        .Where(x => !x.isAcked)
                                                        .Select(x => (long?)x.uniqueTimestampInTicks)
                                                        .FirstOrDefault();

            return firstUnackedMessageTimestamp ?? DateTimeSource.Invoke().Ticks;

            IEnumerable<(bool isAcked, long uniqueTimestampInTicks)> ReadMessages(long bucketId)
            {
                return _dataContext.PersistentMessages
                                   .Where(x => x.PeerId == peerId && x.BucketId == bucketId && x.UniqueTimestampInTicks >= peer.OldestNonAckedMessageTimestampInTicks)
                                   .OrderBy(x => x.UniqueTimestampInTicks)
                                   .Select(x => new { x.IsAcked, x.UniqueTimestampInTicks })
                                   .Execute()
                                   .Select(x => (x.IsAcked, x.UniqueTimestampInTicks));
            }
        }

        public IEnumerable<PeerState> GetAllKnownPeers()
        {
            return _peerStateRepository.GetAllKnownPeers();
        }

        private static long ToUnixMicroSeconds(DateTime timestamp)
        {
            var diff = timestamp - _unixOrigin;
            var diffInMicroSeconds = diff.Ticks / 10;
            return diffInMicroSeconds;
        }
    }
}
