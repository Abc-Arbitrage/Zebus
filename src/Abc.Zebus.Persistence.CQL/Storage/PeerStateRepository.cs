using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.CQL.Data;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Persistence.Util;
using Abc.Zebus.Util;
using Cassandra.Data.Linq;
using log4net;

namespace Abc.Zebus.Persistence.CQL.Storage
{
    public partial class PeerStateRepository : IPeerStateRepository
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(PeerStateRepository));

        private readonly PersistenceCqlDataContext _dataContext;
        private readonly IBus _bus;
        private readonly ConcurrentDictionary<PeerId, PeerState> _statesByPeerId = new ConcurrentDictionary<PeerId, PeerState>();

        public PeerStateRepository(PersistenceCqlDataContext dataContext, IBus bus)
        {
            _dataContext = dataContext;
            _bus = bus;
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
            _statesByPeerId.GetOrAdd(peerId, p =>
            {
                _log.Info($"Create new state for peer {p}");
                return new PeerState(p);
            }).UpdateNonAckedMessageCount(delta);
        }

        public Task Purge(PeerId peerId)
        {
            _log.Info($"Purge queue for peer {peerId} requested");
            PeerState state;
            if (_statesByPeerId.TryRemove(peerId, out state))
            {
                state.Purge();

                var deleteTask = _dataContext.PeerStates.Where(x => x.PeerId == peerId.ToString()).Delete().ExecuteAsync();

                PublishMessageCountForPurgedPeer(state);
                RemoveAllBuckets(state);

                return deleteTask.ContinueWith(t => _log.Info($"Queue for peer {peerId} purged"));
            }

            _log.Info($"Peer to purge not found ({peerId})");
            return Task.FromResult(false);
        }

        private void RemoveAllBuckets(PeerState state)
        {
            var allPossibleBuckets = BucketIdHelper.GetBucketsCollection(SystemDateTime.UtcNow.Ticks - CqlStorage.PersistentMessagesTimeToLive.Ticks).ToArray();

            _dataContext.PersistentMessages.Where(x => x.PeerId == state.PeerId.ToString() && allPossibleBuckets.Contains(x.BucketId))
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