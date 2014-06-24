using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Extensions;
using log4net;

namespace Abc.Zebus.Directory
{
    public partial class PeerDirectoryClient : IPeerDirectory,
        IMessageHandler<PeerStarted>,
        IMessageHandler<PeerStopped>,
        IMessageHandler<PeerDecommissioned>,
        IMessageHandler<PingPeerCommand>,
        IMessageHandler<PeerSubscriptionsUpdated>,
        IMessageHandler<PeerSubscriptionsForTypesUpdated>,
        IMessageHandler<PeerNotResponding>,
        IMessageHandler<PeerResponding>
    {
        private readonly ConcurrentDictionary<MessageTypeId, PeerSubscriptionTree> _subscriptionsByMessageType = new ConcurrentDictionary<MessageTypeId, PeerSubscriptionTree>();
        private readonly ConcurrentDictionary<PeerId, PeerEntry> _peers = new ConcurrentDictionary<PeerId, PeerEntry>();
        private readonly ILog _logger = LogManager.GetLogger(typeof(PeerDirectoryClient));
        private readonly UniqueTimestampProvider _timestampProvider = new UniqueTimestampProvider(10);
        private readonly IBusConfiguration _configuration;

        BlockingCollection<IEvent> _messagesReceivedDuringRegister; 

        private IEnumerable<Peer> _directoryPeers;
        private Peer _self;

        public PeerDirectoryClient(IBusConfiguration configuration)
        {
            _configuration = configuration;
        }

        public event Action<PeerId, PeerUpdateAction> PeerUpdated = delegate { };

        public void Register(IBus bus, Peer self, IEnumerable<Subscription> subscriptions)
        {
            _self = self;

            _subscriptionsByMessageType.Clear();
            _peers.Clear();

            var selfDescriptor = CreateSelfDescriptor(subscriptions);
            AddOrUpdatePeerEntry(selfDescriptor);
         
            _messagesReceivedDuringRegister = new BlockingCollection<IEvent>();

            try
            {
                TryRegisterOnDirectory(bus, selfDescriptor);
            }
            finally
            {
                _messagesReceivedDuringRegister.CompleteAdding();
            }

            ProcessMessagesReceivedDuringRegister();
        }

        private void ProcessMessagesReceivedDuringRegister()
        {
            foreach (dynamic message in _messagesReceivedDuringRegister.GetConsumingEnumerable())
            {
                try
                {
                    Handle(message);
                }
                catch (Exception ex)
                {
                    _logger.WarnFormat("Unable to process message {0} {{{1}}}, Exception: {2}", message.GetType(), message.ToString(), ex);
                }
            }
        }

        private PeerDescriptor CreateSelfDescriptor(IEnumerable<Subscription> subscriptions)
        {
            return new PeerDescriptor(_self.Id, _self.EndPoint, _configuration.IsPersistent, true, true, _timestampProvider.NextUtcTimestamp(), subscriptions.ToArray())
            {
                HasDebuggerAttached = Debugger.IsAttached
            };
        }

        private void TryRegisterOnDirectory(IBus bus, PeerDescriptor selfDescriptor)
        {
            var directoryPeers = GetDirectoryPeers().ToList();
            if (!directoryPeers.Any(peer => TryRegisterOnDirectory(bus, selfDescriptor, peer)))
                throw new TimeoutException(string.Format("Unable to register peer on directory (tried: {0})", string.Join(", ", directoryPeers.Select(peer => "{" + peer + "}"))));
        }

        private bool TryRegisterOnDirectory(IBus bus, PeerDescriptor self, Peer directoryPeer)
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

            var peerEntry = _peers.AddOrUpdate(peerDescriptor.PeerId, (key) => new PeerEntry(peerDescriptor, _subscriptionsByMessageType), (key, entry) =>
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

            peerEntry.SetSubscriptions(subscriptions, peerDescriptor.TimestampUtc);
        }

        public void Handle(PeerStarted message)
        {
            if (EnqueueIfRegistering(message))
                return;

            AddOrUpdatePeerEntry(message.PeerDescriptor);
            PeerUpdated(message.PeerDescriptor.Peer.Id, PeerUpdateAction.Started);
        }

        private bool EnqueueIfRegistering(IEvent message)
        {
            if (_messagesReceivedDuringRegister == null)
                return false;

            if (_messagesReceivedDuringRegister.IsAddingCompleted)
                return false;

            try
            {
                _messagesReceivedDuringRegister.Add(message);
                return true;

            }
            catch (InvalidOperationException)
            {
                // if adding is complete; should only happen in a race
                return false;
            }
        }

        public void Handle(PingPeerCommand message)
        {
        }

        public void Handle(PeerStopped message)
        {
            if (EnqueueIfRegistering(message))
                return;

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
            if (EnqueueIfRegistering(message))
                return;

            PeerEntry removedPeer;
            if (!_peers.TryRemove(message.PeerId, out removedPeer))
                return;

            removedPeer.RemoveSubscriptions();

            PeerUpdated(message.PeerId, PeerUpdateAction.Decommissioned);
        }

        public void Handle(PeerSubscriptionsUpdated message)
        {
            if (EnqueueIfRegistering(message))
                return;
            
            var peer = GetPeerCheckTimestamp(message.PeerDescriptor.Peer.Id, message.PeerDescriptor.TimestampUtc);
            if (peer == null)
                return;

            peer.SetSubscriptions(message.PeerDescriptor.Subscriptions ?? Enumerable.Empty<Subscription>(), message.PeerDescriptor.TimestampUtc);

            peer.Descriptor.Subscriptions = peer.GetSubscriptions();
            peer.Descriptor.TimestampUtc = message.PeerDescriptor.TimestampUtc;

            PeerUpdated(message.PeerDescriptor.PeerId, PeerUpdateAction.Updated);
        }

        public void Handle(PeerSubscriptionsForTypesUpdated message)
        {
            if (EnqueueIfRegistering(message))
                return;

            var peer = _peers.GetValueOrDefault(message.PeerId);
            if (peer == null)
                return;

            peer.SetSubscriptionsForType(message.SubscriptionsForType, message.TimestampUtc);
            
            peer.Descriptor.Subscriptions = peer.GetSubscriptions();

            PeerUpdated(message.PeerId, PeerUpdateAction.Updated);
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
    }
}