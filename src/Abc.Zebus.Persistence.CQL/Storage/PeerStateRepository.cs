using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
    public class PeerStateRepository
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(PeerStateRepository));

        private readonly PersistenceCqlDataContext _dataContext;
        private readonly ConcurrentDictionary<PeerId, PeerState> _statesByPeerId = new ConcurrentDictionary<PeerId, PeerState>();

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

        public Task UpdateNonAckMessageCount(PeerId peerId, int delta)
        {
            var peerState = _statesByPeerId.AddOrUpdate(peerId,
                                                          p =>
                                                          {
                                                              _log.Info($"Created new state for peer {p}");
                                                              return new PeerState(p, delta);
                                                          },
                                                          (id, state) => new PeerState(state.PeerId, state.NonAckedMessageCount + delta, state.OldestNonAckedMessageTimestampInTicks));

            return UpdatePeerState(peerState);
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

        public IEnumerable<PeerState> GetAllKnownPeers()
        {
            return _statesByPeerId.Values;
        }

        public Task UpdateNewOldestMessageTimestamp(PeerState peer, long newOldestMessageTimestamp)
        {
            var updatedPeer = _statesByPeerId.AddOrUpdate(peer.PeerId,
                                                          id => new PeerState(id, 0, newOldestMessageTimestamp),
                                                          (id, state) => new PeerState(id, state.NonAckedMessageCount, newOldestMessageTimestamp));

            return UpdatePeerState(updatedPeer);
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

        private Task<RowSet> UpdatePeerState(PeerState p)
        {
            return _dataContext.PeerStates.Insert(new CassandraPeerState(p)).ExecuteAsync();
        }
    }
}
