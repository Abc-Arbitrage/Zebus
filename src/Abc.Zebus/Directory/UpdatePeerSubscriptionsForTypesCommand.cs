using ProtoBuf;
using System;

namespace Abc.Zebus.Directory
{
    [ProtoContract, Transient]
    public class UpdatePeerSubscriptionsForTypesCommand : ICommand
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly PeerId PeerId;

        [ProtoMember(2, IsRequired = true)]
        public readonly SubscriptionsForType[] SubscriptionsForTypes;

        [ProtoMember(3, IsRequired = false)]
        public readonly DateTime TimestampUtc;

        public UpdatePeerSubscriptionsForTypesCommand(PeerId peerId, DateTime timestampUtc, params SubscriptionsForType[] subscriptionsForTypes)
        {
            PeerId = peerId;
            SubscriptionsForTypes = subscriptionsForTypes;
            TimestampUtc = timestampUtc;
        }

        public override string ToString() => $"{PeerId} TimestampUtc: {TimestampUtc:yyyy-MM-dd HH:mm:ss.fff}";
    }
}