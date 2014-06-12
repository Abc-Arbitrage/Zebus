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
        private readonly ConcurrentDictionary<PeerId, PeerEntry> _peers = new ConcurrentDictionary<PeerId, PeerEntry>();
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
            AddOrUpdatePeerEntry(message.PeerDescriptor);
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

            peer.Descriptor.Peer.IsUp = false;
            peer.Descriptor.Peer.IsResponding = false;
            peer.Descriptor.TimestampUtc = message.TimestampUtc;

            PeerUpdated(message.PeerId, PeerUpdateAction.Stopped);
        }

        public void Handle(PeerDecommissioned message)
        {
            PeerEntry removedPeer;
            if (!_peers.TryRemove(message.PeerId, out removedPeer))
                return;

            foreach (var subscription in removedPeer.SubscriptionStatuses.Where(x => x.Value.Enabled).Select(x => x.Key))
            {
                var messageSubscriptions = _subscriptionsByMessageType.GetValueOrDefault(subscription.MessageTypeId);
                if (messageSubscriptions == null)
                    continue;

                messageSubscriptions.Remove(removedPeer.Descriptor.Peer, subscription);

                if (messageSubscriptions.IsEmpty)
                    _subscriptionsByMessageType.Remove(subscription.MessageTypeId);
            }

            PeerUpdated(message.PeerId, PeerUpdateAction.Decommissioned);
        }

        public void Handle(PeerSubscriptionsAdded message)
        {
            var peer = _peers.GetValueOrDefault(message.PeerId);
            if (peer == null || message.Subscriptions == null)
                return;

            var peerSubscriptions = peer.Descriptor.Subscriptions.ToList();

            foreach (var subscription in message.Subscriptions)
            {
                if (!peer.EnableSubscription(subscription, true, message.TimestampUtc))
                    continue;

                peerSubscriptions.Add(subscription);

                var messageSubscriptions = _subscriptionsByMessageType.GetOrAdd(subscription.MessageTypeId, _ => new PeerSubscriptionTree());
                messageSubscriptions.Add(peer.Descriptor.Peer, subscription);
            }

            peer.Descriptor.Subscriptions = peerSubscriptions.ToArray();

            PeerUpdated(message.PeerId, PeerUpdateAction.Updated);
        }

        public void Handle(PeerSubscriptionsRemoved message)
        {
            var peer = _peers.GetValueOrDefault(message.PeerId);
            if (peer == null || message.Subscriptions == null)
                return;

            var peerSubscriptions = peer.Descriptor.Subscriptions.ToList();

            foreach (var subscription in message.Subscriptions)
            {
                if (!peer.EnableSubscription(subscription, false, message.TimestampUtc))
                    continue;

                peerSubscriptions.Remove(subscription);

                var messageSubscriptions = _subscriptionsByMessageType.GetValueOrDefault(subscription.MessageTypeId);
                if (messageSubscriptions == null)
                    continue;

                messageSubscriptions.Remove(peer.Descriptor.Peer, subscription);
                if (messageSubscriptions.IsEmpty)
                    _subscriptionsByMessageType.Remove(subscription.MessageTypeId);
            }

            peer.Descriptor.Subscriptions = peerSubscriptions.ToArray();

            PeerUpdated(message.PeerId, PeerUpdateAction.Updated);
        }

        public void Handle(PeerSubscriptionsUpdated message)
        {
            var peer = GetPeerCheckTimestamp(message.PeerDescriptor.Peer.Id, message.PeerDescriptor.TimestampUtc);
            if (peer == null)
                return;

            peer.Descriptor.Subscriptions = message.PeerDescriptor.Subscriptions ?? ArrayUtil.Empty<Subscription>();
            peer.Descriptor.TimestampUtc = message.PeerDescriptor.TimestampUtc;

            ReplaceSubscriptions(peer, peer.Descriptor.Subscriptions);

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

            peer.Descriptor.Peer.IsResponding = isResponding;

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

            AddOrUpdatePeerEntry(selfDescriptor);

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
                response.PeerDescriptors.ForEach(AddOrUpdatePeerEntry);

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
            var entry = _peers.GetValueOrDefault(peerId);
            return entry != null ? entry.Descriptor : null;
        }

        public IEnumerable<PeerDescriptor> GetPeerDescriptors()
        {
            return _peers.Values.Select(x => x.Descriptor).ToList();
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

        private void AddOrUpdatePeerEntry(PeerDescriptor peerDescriptor)
        {
            var subscriptions = peerDescriptor.Subscriptions ?? ArrayUtil.Empty<Subscription>();

            var peerEntry = _peers.AddOrUpdate(peerDescriptor.PeerId, (key) => new PeerEntry(peerDescriptor), (key, entry) =>
            {
                entry.Descriptor.Peer.EndPoint = peerDescriptor.Peer.EndPoint;
                entry.Descriptor.Peer.IsUp = peerDescriptor.Peer.IsUp;
                entry.Descriptor.Peer.IsResponding = peerDescriptor.Peer.IsResponding;
                entry.Descriptor.IsPersistent = peerDescriptor.IsPersistent;
                entry.Descriptor.Subscriptions = subscriptions;
                entry.Descriptor.TimestampUtc = peerDescriptor.TimestampUtc;
                entry.Descriptor.HasDebuggerAttached = peerDescriptor.HasDebuggerAttached;

                return entry;
            });

            ReplaceSubscriptions(peerEntry, subscriptions);
        }

        private void ReplaceSubscriptions(PeerEntry peerEntry,  Subscription[] newSubscriptions)
        {
            peerEntry.ClearDisabledSubscriptions();

            var previousSubscriptions = peerEntry.SubscriptionStatuses.Keys.ToList();
            var newSubscriptionSet = newSubscriptions.ToHashSet();

            foreach (var previousSubscription in previousSubscriptions)
            {
                if (newSubscriptionSet.Contains(previousSubscription))
                {
                    newSubscriptionSet.Remove(previousSubscription);
                    continue;
                }

                peerEntry.SubscriptionStatuses.Remove(previousSubscription);

                var messageSubscriptions = _subscriptionsByMessageType.GetValueOrDefault(previousSubscription.MessageTypeId, _ => new PeerSubscriptionTree());
                messageSubscriptions.Remove(peerEntry.Descriptor.Peer, previousSubscription);

                if (messageSubscriptions.IsEmpty)
                    _subscriptionsByMessageType.Remove(previousSubscription.MessageTypeId);
            }

            var timestampUtc = peerEntry.Descriptor.TimestampUtc ?? SystemDateTime.UtcNow;
            foreach (var newSubscription in newSubscriptionSet)
            {
                peerEntry.SubscriptionStatuses.Add(newSubscription, new SubscriptionStatus(true, timestampUtc));

                var messageSubscriptions = _subscriptionsByMessageType.GetOrAdd(newSubscription.MessageTypeId, _ => new PeerSubscriptionTree());
                messageSubscriptions.Add(peerEntry.Descriptor.Peer, newSubscription);
            }
        }

        private PeerEntry GetPeerCheckTimestamp(PeerId peerId, DateTime? timestampUtc)
        {
            var peer = _peers.GetValueOrDefault(peerId);
            if (peer == null)
                return null;

            if (peer.Descriptor.TimestampUtc > timestampUtc)
            {
                _logger.InfoFormat("Outdated message ignored");
                return null;
            }

            return peer;
        }

        private class PeerEntry
        {
            public readonly Dictionary<Subscription, SubscriptionStatus> SubscriptionStatuses = new Dictionary<Subscription, SubscriptionStatus>();
            public readonly PeerDescriptor Descriptor;

            public PeerEntry(PeerDescriptor descriptor)
            {
                Descriptor = descriptor;
            }

            public bool EnableSubscription(Subscription subscription, bool enabled, DateTime timestampUtc)
            {
                SubscriptionStatus status;
                if (SubscriptionStatuses.TryGetValue(subscription, out status))
                {
                    if (status.TimestampUtc > timestampUtc || status.Enabled == enabled)
                        return false;

                    status.Enabled = enabled;
                    status.TimestampUtc = timestampUtc;

                    return true;
                }
                
                SubscriptionStatuses.Add(subscription, new SubscriptionStatus(enabled, timestampUtc));

                return true;
            }

            public void ClearDisabledSubscriptions()
            {
                var disabledSubscriptions = SubscriptionStatuses.Where(x => !x.Value.Enabled).Select(x => x.Key).ToList();
                SubscriptionStatuses.RemoveRange(disabledSubscriptions);
            }
        }

        private class SubscriptionStatus
        {
            public bool Enabled;
            public DateTime TimestampUtc;

            public SubscriptionStatus(bool enabled, DateTime timestampUtc)
            {
                Enabled = enabled;
                TimestampUtc = timestampUtc;
            }
        }
    }
}
