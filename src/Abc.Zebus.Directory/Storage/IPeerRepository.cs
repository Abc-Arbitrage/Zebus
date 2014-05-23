using System.Collections.Generic;

namespace Abc.Zebus.Directory.Storage
{
    public interface IPeerRepository
    {
        void AddOrUpdatePeer(PeerDescriptor peerDescriptor);
        IEnumerable<PeerDescriptor> GetPeers();
        PeerDescriptor Get(PeerId peerId);
        void RemovePeer(PeerId peerId);
        void SetPeerResponding(PeerId peerId, bool isResponding);
    }
}