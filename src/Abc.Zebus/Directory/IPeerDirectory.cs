using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Abc.Zebus.Directory
{
    public interface IPeerDirectory
    {
        event Action<PeerId, PeerUpdateAction> PeerUpdated;
        event Action<PeerId, IReadOnlyList<Subscription>> PeerSubscriptionsUpdated;

        TimeSpan TimeSinceLastPing { get; }

        Task RegisterAsync(IBus bus, Peer self, IEnumerable<Subscription> subscriptions);
        Task UpdateSubscriptionsAsync(IBus bus, IEnumerable<SubscriptionsForType> subscriptionsForTypes);
        Task UnregisterAsync(IBus bus);

        IList<Peer> GetPeersHandlingMessage(IMessage message);
        IList<Peer> GetPeersHandlingMessage(MessageBinding messageBinding);

        bool IsPersistent(PeerId peerId);
        Peer? GetPeer(PeerId peerId);
        void EnableSubscriptionsUpdatedFor(IEnumerable<Type> types);

        // TODO: move to a specific interface (IPeerDirectoryExplorer)
        PeerDescriptor? GetPeerDescriptor(PeerId peerId);

        IEnumerable<PeerDescriptor> GetPeerDescriptors();
    }
}
