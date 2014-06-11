using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Extensions;
using log4net;

namespace Abc.Zebus.Directory
{
    public class PeerDirectoryClient : IPeerDirectory,
        IMessageHandler<PeerStarted>,
        IMessageHandler<PeerStopped>,
        IMessageHandler<PeerDecommissioned>,
        IMessageHandler<PingPeerCommand>,
        IMessageHandler<PeerSubscriptionsAdded>,
        IMessageHandler<PeerSubscriptionsRemoved>,
        IMessageHandler<PeerSubscriptionsUpdated>,
        IMessageHandler<PeerNotResponding>,
        IMessageHandler<PeerResponding>
    {
        private readonly ConcurrentDictionary<MessageTypeId, PeerSubscriptionTree> _subscriptionsByMessageType = new ConcurrentDictionary<MessageTypeId, PeerSubscriptionTree>();
        private readonly ConcurrentDictionary<PeerId, PeerDescriptor> _peers = new ConcurrentDictionary<PeerId, PeerDescriptor>();
        private readonly ILog _logger = LogManager.GetLogger(typeof(PeerDirectoryClient));
        private readonly UniqueTimestampProvider _timestampProvider = new UniqueTimestampProvider();
        private readonly IBusConfiguration _configuration;
        private IEnumerable<Peer> _directoryPeers;
        private Peer _self;

        public PeerDirectoryClient(IBusConfiguration configuration)
        {
            _configuration = configuration;
        }

        public event Action<PeerId, PeerUpdateAction> PeerUpdated = delegate { };

        public void Handle(PeerStarted message)
        {
            LoadPeerDescriptor(message.PeerDescriptor);
            PeerUpdated(message.PeerDescriptor.Peer.Id, PeerUpdateAction.Started);
        }

        public void Handle(PingPeerCommand message)
        {
        }

        public void Handle(PeerStopped message)
        {
            var peer = GetPeerCheckTimestamp(message.PeerId, message.TimestampUtc);
            if (peer == null)
                return;

            peer.Peer.IsUp = false;
            peer.Peer.IsResponding = false;
            peer.TimestampUtc = message.TimestampUtc;

            PeerUpdated(message.PeerId, PeerUpdateAction.Stopped);
        }

        public void Handle(PeerDecommissioned message)
        {
            PeerDescriptor removedPeer;
            if (!_peers.TryRemove(message.PeerId, out removedPeer))
                return;

            // TODO CAO
            UpdatePeerSubscriptions(removedPeer.Peer, removedPeer.Subscriptions, null, DateTime.UtcNow);

            PeerUpdated(message.PeerId, PeerUpdateAction.Decommissioned);
        }

        public void Handle(PeerSubscriptionsAdded message)
        {
            var peer = _peers.GetValueOrDefault(message.PeerId);
            if (peer == null || message.Subscriptions == null)
                return;

            var peerSubscriptions = peer.Subscriptions.ToHashSet();

            foreach (var subscription in message.Subscriptions)
            {
                var messageSubscriptions = _subscriptionsByMessageType.GetOrAdd(subscription.MessageTypeId, _ => new PeerSubscriptionTree());
                if (messageSubscriptions.Add(peer.Peer, subscription, message.TimestampUtc))
                    peerSubscriptions.Add(subscription);
            }

            peer.Subscriptions = peerSubscriptions.ToArray();

            PeerUpdated(message.PeerId, PeerUpdateAction.Updated);
        }

        public void Handle(PeerSubscriptionsRemoved message)
        {
            var peer = _peers.GetValueOrDefault(message.PeerId);
            if (peer == null || message.Subscriptions == null)
                return;

            var peerSubscriptions = peer.Subscriptions.ToHashSet();

            foreach (var subscription in message.Subscriptions)
            {
                var messageSubscriptions = _subscriptionsByMessageType.GetOrAdd(subscription.MessageTypeId, _ => new PeerSubscriptionTree());
                if (messageSubscriptions.Remove(peer.Peer, subscription, message.TimestampUtc))
                    peerSubscriptions.Remove(subscription);
            }

            peer.Subscriptions = peerSubscriptions.ToArray();

            PeerUpdated(message.PeerId, PeerUpdateAction.Updated);
        }

        public void Handle(PeerSubscriptionsUpdated message)
        {
            var peer = GetPeerCheckTimestamp(message.PeerDescriptor.Peer.Id, message.PeerDescriptor.TimestampUtc);
            if (peer == null)
                return;

            var oldSubscriptions = peer.Subscriptions;

            peer.Subscriptions = message.PeerDescriptor.Subscriptions ?? ArrayUtil.Empty<Subscription>();
            peer.TimestampUtc = message.PeerDescriptor.TimestampUtc;

            UpdatePeerSubscriptions(peer.Peer, oldSubscriptions, peer.Subscriptions, peer.TimestampUtc ?? DateTime.UtcNow);

            PeerUpdated(message.PeerDescriptor.PeerId, PeerUpdateAction.Updated);
        }

        public void Handle(PeerNotResponding message)
        {
            HandlePeerRespondingChange(message.PeerId, false);
        }

        public void Handle(PeerResponding message)
        {
            HandlePeerRespondingChange(message.PeerId, true);
        }

        private void HandlePeerRespondingChange(PeerId peerId, bool isResponding)
        {
            var peer = _peers.GetValueOrDefault(peerId);
            if (peer == null)
                return;

            peer.Peer.IsResponding = isResponding;

            PeerUpdated(peerId, PeerUpdateAction.Updated);
        }

        public void Register(IBus bus, Peer self, IEnumerable<Subscription> subscriptions)
        {
            _subscriptionsByMessageType.Clear();
            _peers.Clear();

            _self = self;

            var selfDescriptor = new PeerDescriptor(self.Id, self.EndPoint, _configuration.IsPersistent, true, true, _timestampProvider.NextUtcTimestamp(), subscriptions.ToArray())
            {
                HasDebuggerAttached = Debugger.IsAttached
            };

            LoadPeerDescriptor(selfDescriptor);

            var directoryPeers = GetDirectoryPeers().ToList();
            if (!directoryPeers.Any(peer => TryRegister(bus, selfDescriptor, peer)))
                throw new TimeoutException(string.Format("Unable to register peer on directory (tried: {0})", string.Join(", ", directoryPeers.Select(peer => "{" + peer + "}"))));
        }

        private bool TryRegister(IBus bus, PeerDescriptor self, Peer directoryPeer)
        {
            var registration = bus.Send(new RegisterPeerCommand(self), directoryPeer);
            if (!registration.Wait(_configuration.RegistrationTimeout))
                return false;

            var response = (RegisterPeerResponse)registration.Result.Response;
            if (response == null || response.PeerDescriptors == null)
                return false;

            if (registration.Result.ErrorCode == DirectoryErrorCodes.PeerAlreadyExists)
            {
                _logger.InfoFormat("Register rejected for {0}, the peer already exists in the directory", new RegisterPeerCommand(self).Peer.PeerId);
                return false;
            }

            if (response.PeerDescriptors != null)
                response.PeerDescriptors.ForEach(LoadPeerDescriptor);

            return true;
        }

        public void Update(IBus bus, IEnumerable<Subscription> subscriptions)
        {
            var command = new UpdatePeerSubscriptionsCommand(_self.Id, subscriptions.ToArray(), _timestampProvider.NextUtcTimestamp());
            var directoryPeers = GetDirectoryPeers();
            if (!directoryPeers.Any(peer => bus.Send(command, peer).Wait(5.Seconds())))
                throw new TimeoutException("Unable to update peer subscriptions on directory");
        }

        public void Unregister(IBus bus)
        {
            var command = new UnregisterPeerCommand(_self, _timestampProvider.NextUtcTimestamp());
            // using a cache of the directory peers in case of the underlying configuration proxy values changed before stopping (Abc.gestion...)
            if (!_directoryPeers.Any(peer => bus.Send(command, peer).Wait(5.Seconds())))
                throw new TimeoutException("Unable to unregister peer on directory");
        }

        public IList<Peer> GetPeersHandlingMessage(IMessage message)
        {
            return GetPeersHandlingMessage(MessageBinding.FromMessage(message));
        }

        public IList<Peer> GetPeersHandlingMessage(MessageBinding messageBinding)
        {
            var subscriptionList = _subscriptionsByMessageType.GetValueOrDefault(messageBinding.MessageTypeId);
            if (subscriptionList == null)
                return ArrayUtil.Empty<Peer>();

            return subscriptionList.GetPeers(messageBinding.RoutingKey);
        }

        public PeerDescriptor GetPeerDescriptor(PeerId peerId)
        {
            return _peers.GetValueOrDefault(peerId);
        }

        public IEnumerable<PeerDescriptor> GetPeerDescriptors()
        {
            return _peers.Values;
        }

        // Only internal for testing purposes

        internal IEnumerable<Peer> GetDirectoryPeers()
        {
            _directoryPeers = _configuration.DirectoryServiceEndPoints.Select(CreateDirectoryPeer);
            return _configuration.IsDirectoryPickedRandomly ? _directoryPeers.Shuffle() : _directoryPeers;
        }

        private static Peer CreateDirectoryPeer(string endPoint, int index)
        {
            var peerId = new PeerId("Abc.Zebus.DirectoryService." + index);
            return new Peer(peerId, endPoint);
        }

        private void LoadPeerDescriptor(PeerDescriptor peerDescriptor)
        {
            IEnumerable<Subscription> previousSubscriptions = null;

            var uniquePeerDescriptorInstance = _peers.AddOrUpdate(peerDescriptor.PeerId, peerDescriptor, (key, currentPeer) =>
            {
                previousSubscriptions = currentPeer.Subscriptions;

                currentPeer.Peer.EndPoint = peerDescriptor.Peer.EndPoint;
                currentPeer.Peer.IsUp = peerDescriptor.Peer.IsUp;
                currentPeer.Peer.IsResponding = peerDescriptor.Peer.IsResponding;
                currentPeer.IsPersistent = peerDescriptor.IsPersistent;
                currentPeer.Subscriptions = peerDescriptor.Subscriptions ?? ArrayUtil.Empty<Subscription>();
                currentPeer.TimestampUtc = peerDescriptor.TimestampUtc;
                currentPeer.HasDebuggerAttached = peerDescriptor.HasDebuggerAttached;

                return currentPeer;
            });

            UpdatePeerSubscriptions(uniquePeerDescriptorInstance.Peer, previousSubscriptions, peerDescriptor.Subscriptions, uniquePeerDescriptorInstance.TimestampUtc ?? DateTime.UtcNow);
        }

        private void UpdatePeerSubscriptions(Peer peer, IEnumerable<Subscription> oldSubscriptions, IEnumerable<Subscription> newSubscriptions, DateTime timestampUtc)
        {
            var oldSub = (oldSubscriptions ?? Enumerable.Empty<Subscription>()).ToList();
            var newSub = (newSubscriptions ?? Enumerable.Empty<Subscription>()).ToList();

            var toRemove = oldSub.Except(newSub);
            foreach (var subscription in toRemove)
            {
                var subscriptions = _subscriptionsByMessageType.GetValueOrDefault(subscription.MessageTypeId);
                if (subscriptions == null)
                    continue;

                subscriptions.Remove(peer, subscription, timestampUtc);

                if (subscriptions.IsEmpty)
                    _subscriptionsByMessageType.TryRemove(subscription.MessageTypeId, subscriptions);
            }

            var toAdd = newSub.Except(oldSub);
            foreach (var subscription in toAdd)
            {
                var subscriptions = _subscriptionsByMessageType.GetOrAdd(subscription.MessageTypeId, _ => new PeerSubscriptionTree());
                subscriptions.Add(peer, subscription, timestampUtc);
            }
        }

        private PeerDescriptor GetPeerCheckTimestamp(PeerId peerId, DateTime? timestampUtc)
        {
            var peer = _peers.GetValueOrDefault(peerId);
            if (peer == null)
                return null;

            if (peer.TimestampUtc > timestampUtc)
            {
                _logger.InfoFormat("Outdated message ignored");
                return null;
            }

            return peer;
        }
    }
}
