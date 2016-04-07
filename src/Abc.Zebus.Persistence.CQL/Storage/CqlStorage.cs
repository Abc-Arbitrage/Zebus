using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.CQL.Data;
using Abc.Zebus.Persistence.CQL.Util;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Persistence.Storage;
using Abc.Zebus.Persistence.Util;
using Cassandra;
using log4net;

namespace Abc.Zebus.Persistence.CQL.Storage
{
    public class CqlStorage : IStorage, IDisposable
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(CqlStorage));
        private static readonly Task _completedTask = Task.FromResult(false);

        private readonly PersistenceCqlDataContext _dataContext;
        private readonly IPeerStateRepository _peerStateRepository;
        private readonly IPersistenceConfiguration _configuration;
        private readonly IReporter _reporter;
        private readonly ParallelPersistor _parallelPersistor;
        private readonly PreparedStatement _preparedStatement;

        public CqlStorage(PersistenceCqlDataContext dataContext, IPeerStateRepository peerStateRepository, IPersistenceConfiguration configuration, IReporter reporter)
        {
            _dataContext = dataContext;
            _peerStateRepository = peerStateRepository;
            _configuration = configuration;
            _reporter = reporter;

            _preparedStatement = dataContext.Session.Prepare(dataContext.PersistentMessages.Insert(new PersistentMessage()).SetTTL(0).SetTimestamp(default(DateTimeOffset)).ToString());
            _parallelPersistor = new ParallelPersistor(dataContext.Session, 64, 4 * 64);
        }

        public static TimeSpan PersistentMessagesTimeToLive => 30.Days();

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
                return _completedTask;

            var fattestMessage = entriesToPersist.OrderByDescending(msg => msg.MessageBytes?.Length ?? 0).First();
            _reporter.AddStorageReport(entriesToPersist.Count, entriesToPersist.Sum(msg => msg.MessageBytes?.Length ?? 0), fattestMessage.MessageBytes?.Length ?? 0, fattestMessage.MessageTypeName);

            var insertTasks = new List<Task>(entriesToPersist.Count);
            var countByPeer = new Dictionary<PeerId, int>();
            foreach (var matcherEntry in entriesToPersist)
            {
                var shouldInvestigatePeer = _configuration.PeerIdsToInvestigate != null && _configuration.PeerIdsToInvestigate.Contains(matcherEntry.PeerId.ToString());
                if (shouldInvestigatePeer)
                    _log.Info($"Storage requested for peer {matcherEntry.PeerId}, IsAck: {matcherEntry.IsAck}, Message Id: {matcherEntry.MessageId}"); 

                var messageDateTime = matcherEntry.MessageId.GetDateTime();
                var rowTimestamp = matcherEntry.IsAck ? messageDateTime.AddTicks(10) : messageDateTime;
                var insertTask = _parallelPersistor.Insert(_preparedStatement.Bind(matcherEntry.PeerId.ToString(),
                                                                               BucketIdHelper.GetBucketId(messageDateTime),
                                                                               messageDateTime.Ticks,
                                                                               matcherEntry.IsAck,
                                                                               matcherEntry.MessageBytes,
                                                                               (int)PersistentMessagesTimeToLive.TotalSeconds,
                                                                               ToUnixMicroSeconds(rowTimestamp)));

                if (shouldInvestigatePeer)
                    insertTask = insertTask.ContinueWith(t => _log.Info($"Storage done for peer {matcherEntry.PeerId}, IsAck: {matcherEntry.IsAck}, Message Id: {matcherEntry.MessageId}, TaskResult: {t.Status}"));

                insertTasks.Add(insertTask);
                var countDelta = matcherEntry.IsAck ? -1 : 1;
                if (shouldInvestigatePeer)
                    _log.Info($"Count delta computed for peer {matcherEntry.PeerId}, will increment: {countDelta}");
                countByPeer.AddOrUpdate(matcherEntry.PeerId,  x => countDelta, (peerId, count) => count + countDelta);
            }

            foreach (var countForPeer in countByPeer)
            {
                _peerStateRepository.UpdateNonAckMessageCount(countForPeer.Key, countForPeer.Value);
            }

            return Task.WhenAll(insertTasks);
        }

        public void PurgeMessagesAndAcksForPeer(PeerId peerId)
        {
            _peerStateRepository.Purge(peerId);
        }

        public IMessageReader CreateMessageReader(PeerId peerId)
        {
            _log.Info($"Creating message reader for peer {peerId}");
            var peerState = _peerStateRepository.GetPeerStateFor(peerId);
            var reader = peerState == null ? null : new CqlMessageReader(_dataContext, peerState);
            if (reader == null)
                _log.Info($"PeerState for peer {peerId} does not exist, no reader can be created");
            else
                _log.Info("CqlMessageReader created");
            return reader;
        }

        public void Dispose()
        {
            _parallelPersistor.Dispose();
        }

        private long ToUnixMicroSeconds(DateTime timestamp)
        {
            var origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            var diff = timestamp - origin;
            var diffInMicroSeconds = diff.Ticks / 10;
            return diffInMicroSeconds;
        }
    }
}