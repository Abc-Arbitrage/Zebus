using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Directory;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Extensions;

namespace Abc.Zebus.Testing.Directory
{
    public class TestPeerDirectory : IPeerDirectory
    {
        public readonly ConcurrentDictionary<PeerId, PeerDescriptor> Peers = new ConcurrentDictionary<PeerId, PeerDescriptor>();
        public Peer Self;
        private Peer _remote = new Peer(new PeerId("remote"), "endpoint");

        public event Action Registered = delegate { };
        public event Action<PeerId, PeerUpdateAction> PeerUpdated = delegate { };

        public void Register(IBus bus, Peer self, IEnumerable<Subscription> subscriptions)
        {
            Self = self;
            Peers[self.Id] = self.ToPeerDescriptor(true, subscriptions);

            Registered();
        }

        public void Update(IBus bus, IEnumerable<Subscription> subscriptions)
        {
            Peers[Self.Id] = Self.ToPeerDescriptor(true, subscriptions);
            PeerUpdated(Self.Id, PeerUpdateAction.Updated);
        }

        public void Unregister(IBus bus)
        {
        }

        public void RegisterRemoteListener<TMEssage>() where TMEssage : IMessage
        {
            var peerDescriptor = Peers.GetValueOrAdd(_remote.Id, () => new PeerDescriptor(_remote.Id, _remote.EndPoint, true, _remote.IsUp, _remote.IsResponding, SystemDateTime.UtcNow));
            var subscriptions = new List<Subscription>(peerDescriptor.Subscriptions)
            {
                new Subscription(new MessageTypeId(typeof(TMEssage)))
            };

            peerDescriptor.Subscriptions = subscriptions.ToArray();
        }

        public IList<Peer> GetPeersHandlingMessage(IMessage message)
        {
            return GetPeersHandlingMessage(MessageBinding.FromMessage(message));
        }

        public IList<Peer> GetPeersHandlingMessage(MessageBinding messageBinding)
        {
            return Peers.Where(x => x.Value.Subscriptions.Any(s => s.Matches(messageBinding))).Select(x => x.Value.Peer).ToList();
        }

        public bool IsPersistent(PeerId peerId)
        {
            var peer = Peers.GetValueOrDefault(peerId);
            return peer != null && peer.IsPersistent;
        }

        public PeerDescriptor GetPeerDescriptor(PeerId peerId)
        {
            return Peers.GetValueOrDefault(peerId);
        }

        public IEnumerable<PeerDescriptor> GetPeerDescriptors()
        {
            return Peers.Values;
        }
    }
}