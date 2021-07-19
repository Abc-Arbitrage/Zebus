using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Abc.Zebus.Directory.Configuration;
using Abc.Zebus.Directory.Storage;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Extensions;
using log4net;

namespace Abc.Zebus.Directory.Handlers
{
    public class DirectoryCommandsHandler : IMessageHandler<RegisterPeerCommand>,
                                            IMessageHandler<UpdatePeerSubscriptionsCommand>,
                                            IMessageHandler<UnregisterPeerCommand>,
                                            IMessageHandler<DecommissionPeerCommand>,
                                            IMessageHandler<UpdatePeerSubscriptionsForTypesCommand>,
                                            IMessageHandler<MarkPeerAsRespondingCommand>,
                                            IMessageHandler<MarkPeerAsNotRespondingCommand>,
                                            IMessageContextAware
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(DirectoryCommandsHandler));
        private readonly HashSet<string> _blacklistedMachines;
        private readonly IBus _bus;
        private readonly IPeerRepository _peerRepository;
        private readonly IDirectoryConfiguration _configuration;
        private readonly IDirectorySpeedReporter _speedReporter;

        public DirectoryCommandsHandler(IBus bus, IPeerRepository peerRepository, IDirectoryConfiguration configuration, IDirectorySpeedReporter speedReporter)
        {
            _bus = bus;
            _peerRepository = peerRepository;
            _configuration = configuration;
            _speedReporter = speedReporter;
            _blacklistedMachines = configuration.BlacklistedMachines.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public MessageContext? Context { get; set; }

        public void Handle(DecommissionPeerCommand message)
        {
            RemovePeer(message.PeerId);
        }

        public void Handle(RegisterPeerCommand message)
        {
            if (_blacklistedMachines.Contains(Context!.Originator.SenderMachineName!))
                throw new InvalidOperationException($"Peer {Context.SenderId} on host {Context.Originator.SenderMachineName} is not allowed to register on this directory");

            var peerTimestampUtc = message.Peer.TimestampUtc;
            if (!peerTimestampUtc.HasValue)
                throw new InvalidOperationException("The TimestampUtc must be provided when registering");

            var utcNow = SystemDateTime.UtcNow;
            if (_configuration.MaxAllowedClockDifferenceWhenRegistering != null && peerTimestampUtc.Value > utcNow + _configuration.MaxAllowedClockDifferenceWhenRegistering)
                throw new InvalidOperationException($"The client provided timestamp [{peerTimestampUtc}] is too far ahead of the the server's current time [{utcNow}]");

            var stopwatch = Stopwatch.StartNew();
            var peerDescriptor = message.Peer;
            peerDescriptor.Peer.IsUp = true;
            peerDescriptor.Peer.IsResponding = true;

            var existingPeer = _peerRepository.Get(peerDescriptor.PeerId);
            if (IsPeerInConflict(existingPeer, peerDescriptor))
                throw new MessageProcessingException($"Peer {peerDescriptor.PeerId} already exists (running on {existingPeer.Peer.EndPoint})")
                {
                    ErrorCode = DirectoryErrorCodes.PeerAlreadyExists
                };

            _peerRepository.RemoveAllDynamicSubscriptionsForPeer(peerDescriptor.PeerId, DateTime.SpecifyKind(peerDescriptor.TimestampUtc!.Value, DateTimeKind.Utc));
            _peerRepository.AddOrUpdatePeer(peerDescriptor);
            _bus.Publish(new PeerStarted(peerDescriptor));

            var registredPeerDescriptors = _peerRepository.GetPeers(loadDynamicSubscriptions: true);
            _bus.Reply(new RegisterPeerResponse(registredPeerDescriptors.ToArray()));
            _speedReporter.ReportRegistrationDuration(stopwatch.Elapsed);
        }

        public void Handle(UnregisterPeerCommand message)
        {
            var stopwatch = Stopwatch.StartNew();
            var peer = _peerRepository.Get(message.PeerId);
            if (peer == null || peer.TimestampUtc > message.TimestampUtc)
                return;

            if (peer.IsPersistent)
                StopPeer(message, peer);
            else
                RemovePeer(message.PeerId);
            _speedReporter.ReportUnregistrationDuration(stopwatch.Elapsed);
        }

        public void Handle(UpdatePeerSubscriptionsCommand message)
        {
            var stopwatch = Stopwatch.StartNew();
            var peerDescriptor = _peerRepository.UpdatePeerSubscriptions(message.PeerId, message.Subscriptions, message.TimestampUtc);
            if (peerDescriptor != null)
                _bus.Publish(new PeerSubscriptionsUpdated(peerDescriptor));
            _speedReporter.ReportSubscriptionUpdateDuration(stopwatch.Elapsed);
        }

        private static bool IsPeerInConflict([NotNullWhen(true)] PeerDescriptor? existingPeer, PeerDescriptor peerToAdd)
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

            var stopwatch = Stopwatch.StartNew();

            var subscriptionsToAdd = message.SubscriptionsForTypes.Where(sub => sub.BindingKeys != null && sub.BindingKeys.Any()).ToArray();
            var subscriptionsToRemove = message.SubscriptionsForTypes.Where(sub => sub.BindingKeys == null || !sub.BindingKeys.Any()).ToList();

            if (subscriptionsToAdd.Any())
                _peerRepository.AddDynamicSubscriptionsForTypes(message.PeerId, DateTime.SpecifyKind(message.TimestampUtc, DateTimeKind.Utc), subscriptionsToAdd);
            if (subscriptionsToRemove.Any())
                _peerRepository.RemoveDynamicSubscriptionsForTypes(message.PeerId, DateTime.SpecifyKind(message.TimestampUtc, DateTimeKind.Utc), subscriptionsToRemove.Select(sub => sub.MessageTypeId).ToArray());
            _bus.Publish(new PeerSubscriptionsForTypesUpdated(message.PeerId, message.TimestampUtc, message.SubscriptionsForTypes));

            _speedReporter.ReportSubscriptionUpdateForTypesDuration(stopwatch.Elapsed);
        }

        public void Handle(MarkPeerAsRespondingCommand message)
        {
            _peerRepository.SetPeerResponding(message.PeerId, true);
            _bus.Publish(new PeerResponding(message.PeerId, message.TimestampUtc));
        }

        public void Handle(MarkPeerAsNotRespondingCommand message)
        {
            if (_peerRepository.Get(message.PeerId) == null)
            {
                _log.Warn("MarkPeerAsNotRespondingCommand ignored because the peer cannot be found");
                return;
            }
            _peerRepository.SetPeerResponding(message.PeerId, false);
            _bus.Publish(new PeerNotResponding(message.PeerId, message.TimestampUtc));
        }
    }
}
