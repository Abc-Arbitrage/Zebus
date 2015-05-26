using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Directory.Configuration;
using Abc.Zebus.Directory.Storage;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Extensions;

namespace Abc.Zebus.Directory.Handlers
{
    public class DirectoryCommandsHandler : IMessageHandler<RegisterPeerCommand>,
                                            IMessageHandler<UpdatePeerSubscriptionsCommand>,
                                            IMessageHandler<UnregisterPeerCommand>,
                                            IMessageHandler<DecommissionPeerCommand>,
                                            IMessageHandler<UpdatePeerSubscriptionsForTypesCommand>,
                                            IMessageContextAware
    {
        private readonly HashSet<string> _blacklistedMachines;
        private readonly IBus _bus;
        private readonly IPeerRepository _peerRepository;

        public DirectoryCommandsHandler(IBus bus, IPeerRepository peerRepository, IDirectoryConfiguration configuration)
        {
            _bus = bus;
            _peerRepository = peerRepository;
            _blacklistedMachines = configuration.BlacklistedMachines.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public MessageContext Context { get; set; }

        public void Handle(DecommissionPeerCommand message)
        {
            RemovePeer(message.PeerId);
        }

        public void Handle(RegisterPeerCommand message)
        {
            if (_blacklistedMachines.Contains(Context.Originator.SenderMachineName))
                throw new InvalidOperationException("Peer " + Context.Originator.SenderMachineName + " is not allowed to register on this directory");

            if (!message.Peer.TimestampUtc.HasValue)
                throw new InvalidOperationException("The TimestampUtc must be provided when registering");

            var peerDescriptor = message.Peer;
            peerDescriptor.Peer.IsUp = true;
            peerDescriptor.Peer.IsResponding = true;

            var existingPeer = _peerRepository.Get(peerDescriptor.PeerId);
            if (IsPeerInConflict(existingPeer, peerDescriptor))
                throw new DomainException(DirectoryErrorCodes.PeerAlreadyExists, string.Format("Peer {0} already exists (running on {1})", peerDescriptor.PeerId, existingPeer.Peer.EndPoint));

            _peerRepository.RemoveAllDynamicSubscriptionsForPeer(peerDescriptor.PeerId, DateTime.SpecifyKind(peerDescriptor.TimestampUtc.Value, DateTimeKind.Utc));
            _peerRepository.AddOrUpdatePeer(peerDescriptor);
            _bus.Publish(new PeerStarted(peerDescriptor));

            var registredPeerDescriptors = _peerRepository.GetPeers(loadDynamicSubscriptions: true);
            _bus.Reply(new RegisterPeerResponse(registredPeerDescriptors.ToArray()));
        }

        public void Handle(UnregisterPeerCommand message)
        {
            var peer = _peerRepository.Get(message.PeerId);
            if (peer == null || peer.TimestampUtc > message.TimestampUtc)
                return;

            if (peer.IsPersistent)
                StopPeer(message, peer);
            else
                RemovePeer(message.PeerId);
        }

        public void Handle(UpdatePeerSubscriptionsCommand message)
        {
            var peerDescriptor = _peerRepository.UpdatePeerSubscriptions(message.PeerId, message.Subscriptions, message.TimestampUtc);
            if (peerDescriptor != null)
                _bus.Publish(new PeerSubscriptionsUpdated(peerDescriptor));
        }

        private bool IsPeerInConflict(PeerDescriptor existingPeer, PeerDescriptor peerToAdd)
        {
            return existingPeer != null &&
                   existingPeer.Peer.IsResponding &&
                   existingPeer.PeerId == peerToAdd.PeerId &&
                   existingPeer.Peer.GetMachineNameFromEndPoint() != peerToAdd.Peer.GetMachineNameFromEndPoint();
        }

        private void RemovePeer(PeerId peerId)
        {
            _peerRepository.RemovePeer(peerId);
            _bus.Publish(new PeerDecommissioned(peerId));
        }

        private void StopPeer(UnregisterPeerCommand message, PeerDescriptor peer)
        {
            var peerId = message.PeerId;
            var endPoint = message.PeerEndPoint ?? peer.Peer.EndPoint;
            var timestampUtc = message.TimestampUtc ?? SystemDateTime.UtcNow;

            if (_peerRepository.SetPeerDown(peerId, timestampUtc))
                _bus.Publish(new PeerStopped(peerId, endPoint, timestampUtc));
        }

        public void Handle(UpdatePeerSubscriptionsForTypesCommand message)
        {
            if (message.SubscriptionsForTypes == null || message.SubscriptionsForTypes.Length == 0)
                return;

            var subscriptionsToAdd = message.SubscriptionsForTypes.Where(sub => sub.BindingKeys != null && sub.BindingKeys.Any()).ToArray();
            var subscriptionsToRemove = message.SubscriptionsForTypes.Where(sub => sub.BindingKeys == null || !sub.BindingKeys.Any()).ToList();

            if (subscriptionsToAdd.Any())
                _peerRepository.AddDynamicSubscriptionsForTypes(message.PeerId, DateTime.SpecifyKind(message.TimestampUtc, DateTimeKind.Utc), subscriptionsToAdd);
            if (subscriptionsToRemove.Any())
                _peerRepository.RemoveDynamicSubscriptionsForTypes(message.PeerId, DateTime.SpecifyKind(message.TimestampUtc, DateTimeKind.Utc), subscriptionsToRemove.Select(sub => sub.MessageTypeId).ToArray());
            _bus.Publish(new PeerSubscriptionsForTypesUpdated(message.PeerId, message.TimestampUtc, message.SubscriptionsForTypes));
        }
    }
}