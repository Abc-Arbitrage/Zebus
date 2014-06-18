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
        public readonly SubscriptionsForType[] SubscriptionsForType;

        [ProtoMember(3, IsRequired = false)]
        public readonly DateTime TimestampUtc;

        public UpdatePeerSubscriptionsForTypesCommand(PeerId peerId, DateTime timestampUtc, params SubscriptionsForType[] subscriptionsForType)
        {
            PeerId = peerId;
            SubscriptionsForType = subscriptionsForType;
            TimestampUtc = timestampUtc;
        }

        public override string ToString()
        {
            return string.Format("{0} TimestampUtc: {1:yyyy-MM-dd HH:mm:ss.fff}", PeerId, TimestampUtc);
        }
    }
}