using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.CQL.Data;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Persistence.Util;
using Abc.Zebus.Util;
using Cassandra;
using Cassandra.Data.Linq;
using log4net;

namespace Abc.Zebus.Persistence.CQL.Storage
{
    public class PeerStateRepository : IPeerStateRepository
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(PeerStateRepository));

        private readonly PersistenceCqlDataContext _dataContext;
        private readonly ConcurrentDictionary<PeerId, PeerState> _statesByPeerId = new ConcurrentDictionary<PeerId, PeerState>();
        private long _version;

        public PeerStateRepository(PersistenceCqlDataContext dataContext)
        {
            _dataContext = dataContext;
        }

        public void Initialize()
        {
            _log.Info("Initializing PeerStateRepository");

            foreach (var cassandraPeerState in _dataContext.PeerStates.Execute())
            {
                var peerState = new PeerState(new PeerId(cassandraPeerState.PeerId), cassandraPeerState.NonAckedMessageCount, cassandraPeerState.OldestNonAckedMessageTimestamp);
                _statesByPeerId[peerState.PeerId] = peerState;
            }

            _log.Info($"PeerStateRepository initialized with {_statesByPeerId.Count} states.");
        }

        public PeerState GetPeerStateFor(PeerId peerId)
        {
            return _statesByPeerId.GetValueOrDefault(peerId);
        }

        public PeerState this[PeerId peerId] => _statesByPeerId[peerId];

        public void UpdateNonAckMessageCount(PeerId peerId, int delta)
        {
            var peerState = _statesByPeerId.GetOrAdd(peerId, p =>
            {
                _log.Info($"Create new state for peer {p}");
                return new PeerState(p);
            });

            peerState.NonAckedMessageCount += delta;
            peerState.LastNonAckedMessageCountVersion = Interlocked.Increment(ref _version);
        }

        public List<PeerState> GetUpdatedPeers(ref long version)
        {
            var previousVersion = version;
            var nextVersion = Interlocked.Increment(ref _version);

            var peers = _statesByPeerId.Values
                                       .Where(x => x.LastNonAckedMessageCountVersion >= previousVersion)
                                       .ToList();

            version = nextVersion;

            return peers;
        }

        public Task RemovePeer(PeerId peerId)
        {
            _log.Info($"Purge queue for peer {peerId} requested");

            if (!_statesByPeerId.TryRemove(peerId, out var state))
            {
                _log.Info($"Peer to purge not found ({peerId})");
                return Task.CompletedTask;
            }

            state.MarkAsRemoved();

            var removeTask = Task.WhenAll(DeletePeerState(peerId), RemovePersistentMessages(peerId));
            removeTask.ContinueWith(t => _log.Info($"Queue for peer {peerId} purged"));

            return removeTask;
        }

        private Task<RowSet> DeletePeerState(PeerId peerId)
        {
            return _dataContext.PeerStates.Where(x => x.PeerId == peerId.ToString()).Delete().ExecuteAsync();
        }

        private Task RemovePersistentMessages(PeerId peerId)
        {
            var allPossibleBuckets = BucketIdHelper.GetBucketsCollection(SystemDateTime.UtcNow.Ticks - CqlStorage.PersistentMessagesTimeToLive.Ticks).ToArray();

            return _dataContext.PersistentMessages
                               .Where(x => x.PeerId == peerId.ToString() && allPossibleBuckets.Contains(x.BucketId))
                               .Delete()
                               .ExecuteAsync();
        }

        public Task Save()
        {
            _log.Info("Saving state");
            var tasks = _statesByPeerId.Values
                                       .Select(p => _dataContext.PeerStates.Insert(new CassandraPeerState(p)).ExecuteAsync())
                                       .ToArray();

            return Task.WhenAll(tasks).ContinueWith(t => _log.Info("State saved"));
        }

        public IEnumerator<PeerState> GetEnumerator()
        {
            return _statesByPeerId.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
