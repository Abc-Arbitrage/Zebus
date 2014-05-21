using System;
using System.Collections.Generic;

namespace Abc.Zebus.Directory
{
    public interface IPeerDirectory
    {
        event Action<PeerId, PeerUpdateAction> PeerUpdated;

        void Register(IBus bus, Peer self, IEnumerable<Subscription> subscriptions);
        void Update(IBus bus, IEnumerable<Subscription> subscriptions);
        void Unregister(IBus bus);

        IList<Peer> GetPeersHandlingMessage(IMessage message);
        IList<Peer> GetPeersHandlingMessage(MessageBinding messageBinding);

        PeerDescriptor GetPeerDescriptor(PeerId peerId);
        IEnumerable<PeerDescriptor> GetPeerDescriptors();
    }
}