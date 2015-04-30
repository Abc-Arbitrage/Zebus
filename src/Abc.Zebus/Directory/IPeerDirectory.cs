using System;
using System.Collections.Generic;

namespace Abc.Zebus.Directory
{
    public interface IPeerDirectory
    {
        event Action<PeerId, PeerUpdateAction> PeerUpdated;

        void Register(IBus bus, Peer self, IEnumerable<Subscription> subscriptions);
        void UpdateSubscriptions(IBus bus, IEnumerable<SubscriptionsForType> subscriptionsForTypes);
        void Unregister(IBus bus);

        IList<Peer> GetPeersHandlingMessage(IMessage message);
        IList<Peer> GetPeersHandlingMessage(MessageBinding messageBinding);

        bool IsPersistent(PeerId peerId);

        // TODO: move to a specific interface (IPeerDirectoryExplorer)
        PeerDescriptor GetPeerDescriptor(PeerId peerId);
        IEnumerable<PeerDescriptor> GetPeerDescriptors();
    }
}