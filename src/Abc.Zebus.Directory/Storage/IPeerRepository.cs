using System;
using System.Collections.Generic;

namespace Abc.Zebus.Directory.Storage
{
    public interface IPeerRepository
    {
        bool? IsPersistent(PeerId peerId);
        PeerDescriptor? Get(PeerId peerId);
        IEnumerable<PeerDescriptor> GetPeers(bool loadDynamicSubscriptions = true);

        void AddOrUpdatePeer(PeerDescriptor peerDescriptor);
        void RemovePeer(PeerId peerId);
        void SetPeerResponding(PeerId peerId, bool isResponding);

        void AddDynamicSubscriptionsForTypes(PeerId peerId, DateTime timestampUtc, SubscriptionsForType[] subscriptionsForTypes);
        void RemoveDynamicSubscriptionsForTypes(PeerId peerId, DateTime timestampUtc, MessageTypeId[] messageTypeIds);
        void RemoveAllDynamicSubscriptionsForPeer(PeerId peerId, DateTime timestampUtc);
    }
}
