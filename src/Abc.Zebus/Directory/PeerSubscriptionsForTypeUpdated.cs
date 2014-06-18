using System;
using Abc.Zebus.Routing;
using ProtoBuf;

namespace Abc.Zebus.Directory
{
    [ProtoContract, Transient]
    public sealed class PeerSubscriptionsForTypeUpdated : IEvent
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly PeerId PeerId;

        [ProtoMember(2, IsRequired = true)]
        public readonly MessageTypeId MessageTypeId;

        [ProtoMember(3, IsRequired = true)]
        public readonly BindingKey[] BindingKeys;

        [ProtoMember(4, IsRequired = false)]
        public readonly DateTime TimestampUtc;

        public PeerSubscriptionsForTypeUpdated(PeerId peerId, MessageTypeId messageTypeId, BindingKey[] bindingKeys, DateTime timestampUtc)
        {
            PeerId = peerId;
            MessageTypeId = messageTypeId;
            BindingKeys = bindingKeys;
            TimestampUtc = timestampUtc;
        }

        public override string ToString()
        {
            return string.Format("PeerId: {0}, TimestampUtc: {1:yyyy-MM-dd HH:mm:ss.fff}", PeerId, TimestampUtc);
        }
    }
}