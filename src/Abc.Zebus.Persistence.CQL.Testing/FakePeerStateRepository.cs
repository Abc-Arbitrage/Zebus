using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.CQL.Storage;

namespace Abc.Zebus.Persistence.CQL.Testing
{
    public class FakePeerStateRepository : IPeerStateRepository
    {
        private readonly Dictionary<PeerId, PeerState> _peerStatesByPeerId = new Dictionary<PeerId, PeerState>();
        private long _version;

        public bool IsInitialized { get; set; }
        public bool HasBeenSaved { get; set; }

        public IEnumerator<PeerState> GetEnumerator()
        {
            return _peerStatesByPeerId.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Initialize()
        {
            IsInitialized = true;
        }

        public PeerState GetPeerStateFor(PeerId peerId)
        {
            PeerState peerState;
            return _peerStatesByPeerId.TryGetValue(peerId, out peerState) ? peerState : null;
        }

        public PeerState this[PeerId peerId] => _peerStatesByPeerId[peerId];

        public void Add(PeerState state)
        {
            _peerStatesByPeerId.Add(state.PeerId, state);
        }

        public void UpdateNonAckMessageCount(PeerId peerId, int delta)
        {
            PeerState peerState;
            if (!_peerStatesByPeerId.TryGetValue(peerId, out peerState))
            {
                peerState = new PeerState(peerId);
                _peerStatesByPeerId.Add(peerId, peerState);
            }

            peerState.NonAckedMessageCount += delta;
            peerState.LastNonAckedMessageCountVersion = Interlocked.Increment(ref _version);
        }

        public List<PeerState> GetUpdatedPeers(ref long version)
        {
            var previousVersion = version;
            version = Interlocked.Increment(ref _version);

            return _peerStatesByPeerId.Values
                                      .Where(x => x.LastNonAckedMessageCountVersion >= previousVersion)
                                      .ToList();
        }

        public Task RemovePeer(PeerId peerId)
        {
            var state = GetPeerStateFor(peerId);
            if (state != null)
            {
                state.MarkAsRemoved();
                _peerStatesByPeerId.Remove(peerId);
            }

            return Task.FromResult(0);
        }

        public Task Save()
        {
            HasBeenSaved = true;
            return Task.FromResult(0);
        }
    }
}
