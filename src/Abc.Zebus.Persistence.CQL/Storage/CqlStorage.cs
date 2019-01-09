using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.CQL.Data;
using Abc.Zebus.Persistence.CQL.Util;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Persistence.Reporter;
using Abc.Zebus.Persistence.Storage;
using Abc.Zebus.Persistence.Util;
using Abc.Zebus.Util;
using Cassandra;
using Cassandra.Data.Linq;
using log4net;

namespace Abc.Zebus.Persistence.CQL.Storage
{
    public class CqlStorage : ICqlStorage, IDisposable
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(CqlStorage));
        private static readonly DateTime _unixOrigin = new DateTime(1970, 1, 1, 0, 0, 0, 0);

        private readonly PersistenceCqlDataContext _dataContext;
        private readonly IPeerStateRepository _peerStateRepository;
        private readonly IPersistenceConfiguration _configuration;
        private readonly IReporter _reporter;
        private readonly ParallelPersistor _parallelPersistor;
        private readonly PreparedStatement _preparedStatement;
        private long _lastNonAckedMessageCountsVersion;

        public CqlStorage(PersistenceCqlDataContext dataContext, IPeerStateRepository peerStateRepository, IPersistenceConfiguration configuration, IReporter reporter)
        {
            _dataContext = dataContext;
            _peerStateRepository = peerStateRepository;
            _configuration = configuration;
            _reporter = reporter;

            _preparedStatement = dataContext.Session.Prepare(dataContext.PersistentMessages.Insert(new PersistentMessage()).SetTTL(0).SetTimestamp(default(DateTimeOffset)).ToString());
            _parallelPersistor = new ParallelPersistor(dataContext.Session, 64, ex => _log.Warn("Exception caught while trying to persist message", ex));
        }

        public static TimeSpan PersistentMessagesTimeToLive => 30.Days();

        public int PersistenceQueueSize => _parallelPersistor.QueueSize;

        public Dictionary<PeerId, int> GetNonAckedMessageCountsForUpdatedPeers()
        {
            return _peerStateRepository.GetUpdatedPeers(ref _lastNonAckedMessageCountsVersion)
                                       .ToDictionary(x => x.PeerId, x => x.NonAckedMessageCount);
        }

        public void Start()
        {
            _peerStateRepository.Initialize();
            _parallelPersistor.Start();
        }

        public void Stop()
        {
            Dispose();
            _peerStateRepository.Save().Wait(2.Seconds());
        }

        public Task Write(IList<MatcherEntry> entriesToPersist)
        {
            if (entriesToPersist.Count == 0)
                return Task.CompletedTask;

            var fattestMessage = entriesToPersist.OrderByDescending(msg => msg.MessageBytes?.Length ?? 0).First();
            _reporter.AddStorageReport(entriesToPersist.Count, entriesToPersist.Sum(msg => msg.MessageBytes?.Length ?? 0), fattestMessage.MessageBytes?.Length ?? 0, fattestMessage.MessageTypeName);

            var insertTasks = new List<Task>(entriesToPersist.Count);
            var countByPeer = new Dictionary<PeerId, int>();
            foreach (var matcherEntry in entriesToPersist)
            {
                var shouldInvestigatePeer = _configuration.PeerIdsToInvestigate != null && _configuration.PeerIdsToInvestigate.Contains(matcherEntry.PeerId.ToString());
                if (shouldInvestigatePeer)
                    _log.Info($"Storage requested for peer {matcherEntry.PeerId}, Type: {matcherEntry.Type}, Message Id: {matcherEntry.MessageId}"); 

                var messageDateTime = matcherEntry.MessageId.GetDateTimeForV2OrV3();
                var rowTimestamp = matcherEntry.IsAck ? messageDateTime.AddTicks(10) : messageDateTime;
                var insertTask = _parallelPersistor.Insert(_preparedStatement.Bind(matcherEntry.PeerId.ToString(),
                                                                               BucketIdHelper.GetBucketId(messageDateTime),
                                                                               messageDateTime.Ticks,
                                                                               matcherEntry.MessageId.Value,
                                                                               matcherEntry.IsAck,
                                                                               matcherEntry.MessageBytes,
                                                                               (int)PersistentMessagesTimeToLive.TotalSeconds,
                                                                               ToUnixMicroSeconds(rowTimestamp)));

                if (shouldInvestigatePeer)
                    insertTask = insertTask.ContinueWith(t => _log.Info($"Storage done for peer {matcherEntry.PeerId}, Type: {matcherEntry.Type}, Message Id: {matcherEntry.MessageId}, TaskResult: {t.Status}"));

                insertTasks.Add(insertTask);
                var countDelta = matcherEntry.IsAck ? -1 : 1;
                countByPeer[matcherEntry.PeerId] = countDelta + countByPeer.GetValueOrDefault(matcherEntry.PeerId);

                if (shouldInvestigatePeer)
                    _log.Info($"Count delta computed for peer {matcherEntry.PeerId}, will increment: {countDelta}");
            }

            foreach (var countForPeer in countByPeer)
            {
                _peerStateRepository.UpdateNonAckMessageCount(countForPeer.Key, countForPeer.Value);
            }

            return Task.WhenAll(insertTasks);
        }

        public void RemovePeer(PeerId peerId)
        {
            _peerStateRepository.RemovePeer(peerId);
        }

        public IMessageReader CreateMessageReader(PeerId peerId)
        {
            _log.Info($"Creating message reader for peer {peerId}");
            var peerState = _peerStateRepository.GetPeerStateFor(peerId);
            if (peerState == null)
            {
                _log.Info($"PeerState for peer {peerId} does not exist, no reader can be created");
                return null;
            }

            var reader = new CqlMessageReader(_dataContext, peerState);
            _log.Info("CqlMessageReader created");

            return reader;
        }

        public void Dispose()
        {
            _parallelPersistor.Dispose();
        }

        public long GetOldestNonAckedMessageTimestamp(PeerState peer)
        {
            var peerId = peer.PeerId.ToString();
            var lastAckedMessageTimestamp = 0L;

            foreach (var currentBucketId in BucketIdHelper.GetBucketsCollection(peer.OldestNonAckedMessageTimestampInTicks))
            {
                var messagesInBucket = _dataContext.PersistentMessages
                                                   .Where(x => x.PeerId == peerId
                                                               && x.BucketId == currentBucketId
                                                               && x.UniqueTimestampInTicks >= peer.OldestNonAckedMessageTimestampInTicks)
                                                   .OrderBy(x => x.UniqueTimestampInTicks)
                                                   .Select(x => new { x.IsAcked, x.UniqueTimestampInTicks })
                                                   .Execute();

                foreach (var message in messagesInBucket)
                {
                    if (!message.IsAcked)
                        return message.UniqueTimestampInTicks;

                    lastAckedMessageTimestamp = message.UniqueTimestampInTicks;
                    
                }
            }

            return lastAckedMessageTimestamp == 0 ? SystemDateTime.UtcNow.Ticks : lastAckedMessageTimestamp + 1;
        }

        public void CleanBuckets(PeerId peerId, long previousOldestMessageTimestamp, long newOldestMessageTimestamp)
        {
            var firstBucketToDelete = BucketIdHelper.GetBucketId(previousOldestMessageTimestamp);
            var lastBucketToDelete = BucketIdHelper.GetPreviousBucketId(newOldestMessageTimestamp);
            if (firstBucketToDelete == lastBucketToDelete)
                return;

            var bucketsToDelete = BucketIdHelper.GetBucketsCollection(firstBucketToDelete, lastBucketToDelete).ToArray();
            _dataContext.PersistentMessages
                        .Where(x => x.PeerId == peerId.ToString() && bucketsToDelete.Contains(x.BucketId))
                        .Delete()
                        .ExecuteAsync();
        }

        private static long ToUnixMicroSeconds(DateTime timestamp)
        {
            var diff = timestamp - _unixOrigin;
            var diffInMicroSeconds = diff.Ticks / 10;
            return diffInMicroSeconds;
        }
    }
}
