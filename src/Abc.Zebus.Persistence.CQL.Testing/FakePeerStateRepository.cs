using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.CQL.Storage;
using Abc.Zebus.Persistence.Messages;

namespace Abc.Zebus.Persistence.CQL.Testing
{
    public class FakePeerStateRepository : IPeerStateRepository
    {
        private readonly Dictionary<PeerId, PeerState> _peerStatesByPeerId = new Dictionary<PeerId, PeerState>();

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

        public void Handle(PublishNonAckMessagesCountCommand message)
        {
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

        public void Remove(PeerId peerId)
        {
            _peerStatesByPeerId.Remove(peerId);
        }

        public void UpdateNonAckMessageCount(PeerId peerId, int delta)
        {
            PeerState peerState;
            if (!_peerStatesByPeerId.TryGetValue(peerId, out peerState))
            {
                peerState = new PeerState(peerId);
                _peerStatesByPeerId.Add(peerId, peerState);
            }
            peerState.UpdateNonAckedMessageCount(delta);
        }

        public Task Purge(PeerId peerId)
        {
            var state = GetPeerStateFor(peerId);
            if (state != null)
            {
                state.Purge();
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