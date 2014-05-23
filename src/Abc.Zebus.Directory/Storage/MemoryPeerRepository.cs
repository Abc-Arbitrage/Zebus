using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Abc.Zebus.Directory.Storage
{
    public class MemoryPeerRepository : IPeerRepository
    {
        private readonly ConcurrentDictionary<PeerId, PeerDescriptor> _peers = new ConcurrentDictionary<PeerId, PeerDescriptor>();
 
        public void AddOrUpdatePeer(PeerDescriptor peerDescriptor)
        {
            _peers.AddOrUpdate(peerDescriptor.PeerId, peerDescriptor, (peerId, existingPeer) => peerDescriptor.TimestampUtc >= existingPeer.TimestampUtc ? peerDescriptor : existingPeer);
        }

        public IEnumerable<PeerDescriptor> GetPeers()
        {
            return _peers.Values;
        }

        public PeerDescriptor Get(PeerId peerId)
        {
            PeerDescriptor descriptor;
            return _peers.TryGetValue(peerId, out descriptor) ? descriptor : null;
        }

        public void RemovePeer(PeerId peerId)
        {
            PeerDescriptor unused;
            _peers.TryRemove(peerId, out unused);
        }

        public void SetPeerResponding(PeerId peerId, bool isResponding)
        {
            var peer = Get(peerId);
            if (peer != null)
                peer.Peer.IsResponding = isResponding;
        }
    }
}
