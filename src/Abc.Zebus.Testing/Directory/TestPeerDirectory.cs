using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Directory;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Extensions;

namespace Abc.Zebus.Testing.Directory
{
    public class TestPeerDirectory : IPeerDirectory
    {
        private Subscription[] _initialSubscriptions = Array.Empty<Subscription>();
        private readonly Dictionary<MessageTypeId, SubscriptionsForType> _dynamicSubscriptions = new Dictionary<MessageTypeId, SubscriptionsForType>();

        public TestPeerDirectory()
        {
        }

        public event Action Registered = delegate { };
        public event Action<PeerId, PeerUpdateAction> PeerUpdated = delegate { };
        public event Action<PeerId, IReadOnlyList<Subscription>> PeerSubscriptionsUpdated = delegate { };

        public ConcurrentDictionary<PeerId, PeerDescriptor> Peers { get; } = new ConcurrentDictionary<PeerId, PeerDescriptor>();
        public Peer? Self { get; private set; }

        public TimeSpan TimeSinceLastPing => TimeSpan.Zero;

        public Task RegisterAsync(IBus bus, Peer self, IEnumerable<Subscription> subscriptions)
        {
            var subscriptionArray = subscriptions.ToArray();

            Self = self;
            Self.IsResponding = true;
            Self.IsUp = true;
            Peers[self.Id] = self.ToPeerDescriptor(true, subscriptionArray);

            _initialSubscriptions = subscriptionArray;

            Registered();
            return Task.CompletedTask;
        }

        public Task UpdateSubscriptionsAsync(IBus bus, IEnumerable<SubscriptionsForType> subscriptionsForTypes)
        {
            foreach (var subscriptionsForType in subscriptionsForTypes)
            {
                _dynamicSubscriptions[subscriptionsForType.MessageTypeId] = subscriptionsForType;
            }

            var newSubscriptions = _initialSubscriptions.Concat(_dynamicSubscriptions.SelectMany(x => x.Value.ToSubscriptions()));

            Peers[Self!.Id] = Self.ToPeerDescriptor(true, newSubscriptions);
            PeerUpdated(Self.Id, PeerUpdateAction.Updated);
            return Task.CompletedTask;
        }

        public Task UnregisterAsync(IBus bus)
        {
            _initialSubscriptions = Array.Empty<Subscription>();
            _dynamicSubscriptions.Clear();

            Peers[Self!.Id] = Self.ToPeerDescriptor(true);
            PeerUpdated(Self!.Id, PeerUpdateAction.Stopped);
            return Task.CompletedTask;
        }

        private readonly Peer _remote = new Peer(new PeerId("remote"), "endpoint");

        [Obsolete("Use SetupPeer(new PeerId(\"Abc.Remote.0\"), Subscription.Any<TMessage>()) instead")]
        public void RegisterRemoteListener<TMessage>()
            where TMessage : IMessage
        {
            var peerDescriptor = Peers.GetValueOrAdd(_remote.Id, () => new PeerDescriptor(_remote.Id, _remote.EndPoint, true, _remote.IsUp, _remote.IsResponding, SystemDateTime.UtcNow));
            var subscriptions = new List<Subscription>(peerDescriptor.Subscriptions)
            {
                new Subscription(new MessageTypeId(typeof(TMessage)))
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
            return Peers.TryGetValue(peerId, out var peer) && peer.IsPersistent;
        }

        public Peer? GetPeer(PeerId peerId)
        {
            return Peers.TryGetValue(peerId, out var peerDescriptor) ? peerDescriptor.Peer : null;
        }

        public void EnableSubscriptionsUpdatedFor(IEnumerable<Type> types)
        {
        }

        public PeerDescriptor? GetPeerDescriptor(PeerId peerId)
        {
            return Peers.TryGetValue(peerId, out var peer)
                ? peer
                : null;
        }

        public IEnumerable<PeerDescriptor> GetPeerDescriptors()
        {
            return Peers.Values;
        }

        public IEnumerable<Subscription> GetSelfSubscriptions()
        {
            return Self != null && GetPeerDescriptor(Self.Id) is { } descriptor
                ? descriptor.Subscriptions
                : Array.Empty<Subscription>();
        }

        public void SetupPeer(PeerId peerId, params Subscription[] subscriptions)
        {
            var peerNumber = Peers.Count;
            var randomPort = 10000 + Peers.Count;
            SetupPeer(new Peer(peerId, $"tcp://testing-peer-{peerNumber}:{randomPort}"), subscriptions);
        }

        public void SetupPeer(Peer peer, params Subscription[] subscriptions)
        {
            var descriptor = Peers.GetOrAdd(peer.Id, _ => peer.ToPeerDescriptor(true));
            descriptor.Peer.IsResponding = peer.IsResponding;
            descriptor.Peer.IsUp = peer.IsUp;
            descriptor.Peer.EndPoint = peer.EndPoint;
            descriptor.TimestampUtc = DateTime.UtcNow;
            descriptor.Subscriptions = subscriptions;
        }
    }
}
