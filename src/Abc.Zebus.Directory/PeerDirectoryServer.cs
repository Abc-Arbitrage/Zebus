using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Abc.Zebus.Directory.Configuration;
using Abc.Zebus.Directory.Storage;
using Abc.Zebus.Util;

namespace Abc.Zebus.Directory
{
    public class PeerDirectoryServer : IPeerDirectory,
                                       IMessageHandler<PeerStarted>,
                                       IMessageHandler<PeerStopped>,
                                       IMessageHandler<PeerDecommissioned>,
                                       IMessageHandler<PingPeerCommand>,
                                       IMessageHandler<PeerSubscriptionsUpdated>,
                                       IMessageHandler<PeerNotResponding>,
                                       IMessageHandler<PeerResponding>
    {
        private readonly IDirectoryConfiguration _configuration;
        private readonly IPeerRepository _peerRepository;
        private Peer _self;

        public PeerDirectoryServer(IDirectoryConfiguration configuration, IPeerRepository peerRepository)
        {
            _configuration = configuration;
            _peerRepository = peerRepository;
        }

        public event Action Registered = delegate { };
        public event Action<PeerId, PeerUpdateAction> PeerUpdated = delegate { };

        public IList<Peer> GetPeersHandlingMessage(IMessage message)
        {
            return GetPeersHandlingMessage(MessageBinding.FromMessage(message));
        }

        public IList<Peer> GetPeersHandlingMessage(MessageBinding messageBinding)
        {
            return _peerRepository.GetPeers(!_configuration.DisableDynamicSubscriptionsForDirectoryOutgoingMessages)
                                  .Where(peer => peer.Subscriptions != null && peer.Subscriptions.Any(x => x.MessageTypeId == messageBinding.MessageTypeId && x.Matches(messageBinding.RoutingKey)))
                                  .Select(peerDesc => peerDesc.Peer)
                                  .ToList();
        }

        public bool IsPersistent(PeerId peerId)
        {
            // TODO: avoid loading subscriptions

            var peer = _peerRepository.Get(peerId);
            return peer != null && peer.IsPersistent;
        }

        public PeerDescriptor GetPeerDescriptor(PeerId peerId)
        {
            return _peerRepository.Get(peerId);
        }

        public IEnumerable<PeerDescriptor> GetPeerDescriptors()
        {
            return _peerRepository.GetPeers();
        }

        public void Register(IBus bus, Peer self, IEnumerable<Subscription> subscriptions)
        {
            _self = self;

            var selfDescriptor = new PeerDescriptor(self.Id, self.EndPoint, false, self.IsUp, self.IsResponding, SystemDateTime.UtcNow, subscriptions.ToArray())
            {
                HasDebuggerAttached = Debugger.IsAttached
            };

            _peerRepository.AddOrUpdatePeer(selfDescriptor);

            bus.Publish(new PeerStarted(selfDescriptor));

            Registered();
        }

        public void UpdateSubscriptions(IBus bus, IEnumerable<SubscriptionsForType> subscriptionsForTypes)
        {
            var subsForTypes = subscriptionsForTypes.ToList();
            var subscriptionsToAdd = subsForTypes.Where(sub => sub.BindingKeys != null && sub.BindingKeys.Any()).ToArray();
            var subscriptionsToRemove = subsForTypes.Where(sub => sub.BindingKeys == null || !sub.BindingKeys.Any()).ToList();

            var utcNow = SystemDateTime.UtcNow;
            if (subscriptionsToAdd.Any())
                _peerRepository.AddDynamicSubscriptionsForTypes(_self.Id, utcNow, subscriptionsToAdd);

            if (subscriptionsToRemove.Any())
                _peerRepository.RemoveDynamicSubscriptionsForTypes(_self.Id, utcNow, subscriptionsToRemove.Select(sub => sub.MessageTypeId).ToArray());

            bus.Publish(new PeerSubscriptionsForTypesUpdated(_self.Id, utcNow, subsForTypes.ToArray()));
        }

        public void Unregister(IBus bus)
        {
            _peerRepository.SetPeerDown(_self.Id, SystemDateTime.UtcNow);
            bus.Publish(new PeerStopped(_self));
        }

        public void Handle(PeerStarted message)
        {
            PeerUpdated(message.PeerDescriptor.PeerId, PeerUpdateAction.Started);
        }

        public void Handle(PeerStopped message)
        {
            PeerUpdated(message.PeerId, PeerUpdateAction.Stopped);
        }

        public void Handle(PeerDecommissioned message)
        {
            PeerUpdated(message.PeerId, PeerUpdateAction.Decommissioned);
        }

        public void Handle(PingPeerCommand message)
        {
        }

        public void Handle(PeerSubscriptionsUpdated message)
        {
            PeerUpdated(message.PeerDescriptor.PeerId, PeerUpdateAction.Updated);
        }

        public void Handle(PeerNotResponding message)
        {
            PeerUpdated(message.PeerId, PeerUpdateAction.Updated);
        }

        public void Handle(PeerResponding message)
        {
            PeerUpdated(message.PeerId, PeerUpdateAction.Updated);
        }
    }
}