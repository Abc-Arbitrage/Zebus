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
        private readonly ConcurrentDictionary<MessageTypeId, PeerSubscriptionTree> _globalSubscriptionsIndex = new ConcurrentDictionary<MessageTypeId, PeerSubscriptionTree>();
        private readonly ConcurrentDictionary<PeerId, PeerEntry> _peers = new ConcurrentDictionary<PeerId, PeerEntry>();
        private readonly ILog _logger = LogManager.GetLogger(typeof(PeerDirectoryClient));
        private readonly UniqueTimestampProvider _timestampProvider = new UniqueTimestampProvider(10);
        private readonly IBusConfiguration _configuration;
        private BlockingCollection<IEvent> _messagesReceivedDuringRegister;
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

            _globalSubscriptionsIndex.Clear();
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

        public void UpdateSubscriptions(IBus bus, IEnumerable<SubscriptionsForType> subscriptionsForTypes)
        {
            var subscriptions = subscriptionsForTypes as SubscriptionsForType[] ?? subscriptionsForTypes.ToArray();
            if (subscriptions.Length == 0)
                return;

            var command = new UpdatePeerSubscriptionsForTypesCommand(_self.Id, _timestampProvider.NextUtcTimestamp(), subscriptions);
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
            var subscriptionList = _globalSubscriptionsIndex.GetValueOrDefault(messageBinding.MessageTypeId);
            if (subscriptionList == null)
                return ArrayUtil.Empty<Peer>();

            return subscriptionList.GetPeers(messageBinding.RoutingKey);
        }

        public bool IsPersistent(PeerId peerId)
        {
            var entry = _peers.GetValueOrDefault(peerId);
            return entry != null && entry.IsPersistent;
        }

        public PeerDescriptor GetPeerDescriptor(PeerId peerId)
        {
            var entry = _peers.GetValueOrDefault(peerId);
            return entry != null ? entry.ToPeerDescriptor() : null;
        }

        public IEnumerable<PeerDescriptor> GetPeerDescriptors()
        {
            return _peers.Values.Select(x => x.ToPeerDescriptor()).ToList();
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

            var peerEntry = _peers.AddOrUpdate(peerDescriptor.PeerId, key => new PeerEntry(peerDescriptor, _globalSubscriptionsIndex), (key, entry) =>
            {
                entry.Peer.EndPoint = peerDescriptor.Peer.EndPoint;
                entry.Peer.IsUp = peerDescriptor.Peer.IsUp;
                entry.Peer.IsResponding = peerDescriptor.Peer.IsResponding;
                entry.IsPersistent = peerDescriptor.IsPersistent;
                entry.TimestampUtc = peerDescriptor.TimestampUtc ?? DateTime.UtcNow;
                entry.HasDebuggerAttached = peerDescriptor.HasDebuggerAttached;

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
            if (peer.Value == null)
                return;

            peer.Value.Peer.IsUp = false;
            peer.Value.Peer.IsResponding = false;
            peer.Value.TimestampUtc = message.TimestampUtc ?? DateTime.UtcNow;

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
            if (peer.Value == null)
            {
                WarnWhenPeerDoesNotExist(peer, message.PeerDescriptor.PeerId);
                return;
            }

            peer.Value.SetSubscriptions(message.PeerDescriptor.Subscriptions ?? Enumerable.Empty<Subscription>(), message.PeerDescriptor.TimestampUtc);
            peer.Value.TimestampUtc = message.PeerDescriptor.TimestampUtc ?? DateTime.UtcNow;

            PeerUpdated(message.PeerDescriptor.PeerId, PeerUpdateAction.Updated);
        }

        public void Handle(PeerSubscriptionsForTypesUpdated message)
        {
            if (EnqueueIfRegistering(message))
                return;

            var peer = GetPeerCheckTimestamp(message.PeerId, message.TimestampUtc);
            if (peer.Value == null)
            {
                WarnWhenPeerDoesNotExist(peer, message.PeerId);
                return;
            }

            peer.Value.SetSubscriptionsForType(message.SubscriptionsForType ?? Enumerable.Empty<SubscriptionsForType>(), message.TimestampUtc);

            PeerUpdated(message.PeerId, PeerUpdateAction.Updated);
        }

        private void WarnWhenPeerDoesNotExist(PeerEntryResult peer, PeerId peerId)
        {
            if (peer.FailureReason == PeerEntryResult.FailureReasonType.PeerNotPresent)
                _logger.WarnFormat("Received message but no peer existed: {0}", peerId);
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

        private PeerEntryResult GetPeerCheckTimestamp(PeerId peerId, DateTime? timestampUtc)
        {
            var peer = _peers.GetValueOrDefault(peerId);
            if (peer == null)
                return new PeerEntryResult(PeerEntryResult.FailureReasonType.PeerNotPresent);

            if (peer.TimestampUtc > timestampUtc)
            {
                _logger.InfoFormat("Outdated message ignored");
                return new PeerEntryResult(PeerEntryResult.FailureReasonType.OutdatedMessage);
            }

            return new PeerEntryResult(peer);
        }

        struct PeerEntryResult
        {
            internal enum FailureReasonType
            {
                PeerNotPresent,
                OutdatedMessage,
            }

            public PeerEntryResult(PeerEntry value) : this()
            {
                Value = value;
            }

            public PeerEntryResult(FailureReasonType failureReason) : this()
            {
                FailureReason = failureReason;
            }

            public PeerEntry Value { get; private set; }
            public FailureReasonType? FailureReason { get; private set; }
        }
    }
}