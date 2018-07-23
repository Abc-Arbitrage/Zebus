using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
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
        private readonly Stopwatch _pingStopwatch = new Stopwatch();
        private Peer _self;

        public PeerDirectoryServer(IDirectoryConfiguration configuration, IPeerRepository peerRepository)
        {
            _configuration = configuration;
            _peerRepository = peerRepository;
        }

        public event Action Registered;
        public event Action<PeerId, PeerUpdateAction> PeerUpdated;

        public TimeSpan TimeSinceLastPing => _pingStopwatch.IsRunning ? _pingStopwatch.Elapsed : TimeSpan.MaxValue;

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
            return _peerRepository.IsPersistent(peerId).GetValueOrDefault();
        }

        public PeerDescriptor GetPeerDescriptor(PeerId peerId)
        {
            return _peerRepository.Get(peerId);
        }

        public IEnumerable<PeerDescriptor> GetPeerDescriptors()
        {
            return _peerRepository.GetPeers();
        }

        public Task RegisterAsync(IBus bus, Peer self, IEnumerable<Subscription> subscriptions)
        {
            _self = self;

            var selfDescriptor = new PeerDescriptor(self.Id, self.EndPoint, false, self.IsUp, self.IsResponding, SystemDateTime.UtcNow, subscriptions.ToArray())
            {
                HasDebuggerAttached = Debugger.IsAttached
            };

            _peerRepository.AddOrUpdatePeer(selfDescriptor);
            _pingStopwatch.Restart();

            bus.Publish(new PeerStarted(selfDescriptor));

            Registered?.Invoke();

            return Task.CompletedTask;
        }

        public Task UpdateSubscriptionsAsync(IBus bus, IEnumerable<SubscriptionsForType> subscriptionsForTypes)
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

            return Task.CompletedTask;
        }

        public Task UnregisterAsync(IBus bus)
        {
            _peerRepository.SetPeerDown(_self.Id, SystemDateTime.UtcNow);
            bus.Publish(new PeerStopped(_self));
            _pingStopwatch.Stop();

            return Task.CompletedTask;
        }

        public void Handle(PeerStarted message)
        {
            PeerUpdated?.Invoke(message.PeerDescriptor.PeerId, PeerUpdateAction.Started);
        }

        public void Handle(PeerStopped message)
        {
            PeerUpdated?.Invoke(message.PeerId, PeerUpdateAction.Stopped);
        }

        public void Handle(PeerDecommissioned message)
        {
            PeerUpdated?.Invoke(message.PeerId, PeerUpdateAction.Decommissioned);
        }

        public void Handle(PingPeerCommand message)
        {
            if (_pingStopwatch.IsRunning)
                _pingStopwatch.Restart();
        }

        public void Handle(PeerSubscriptionsUpdated message)
        {
            PeerUpdated?.Invoke(message.PeerDescriptor.PeerId, PeerUpdateAction.Updated);
        }

        public void Handle(PeerNotResponding message)
        {
            PeerUpdated?.Invoke(message.PeerId, PeerUpdateAction.Updated);
        }

        public void Handle(PeerResponding message)
        {
            PeerUpdated?.Invoke(message.PeerId, PeerUpdateAction.Updated);
        }
    }
}
