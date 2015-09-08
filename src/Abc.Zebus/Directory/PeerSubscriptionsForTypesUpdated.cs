using Abc.Zebus.Routing;
using ProtoBuf;
using System;

namespace Abc.Zebus.Directory
{
    [ProtoContract, Transient]
    public sealed class PeerSubscriptionsForTypesUpdated : IEvent
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly PeerId PeerId;

        [ProtoMember(2, IsRequired = true)]
        public readonly SubscriptionsForType[] SubscriptionsForType;

        [ProtoMember(3, IsRequired = false)]
        public readonly DateTime TimestampUtc;

        public PeerSubscriptionsForTypesUpdated(PeerId peerId, DateTime timestampUtc, MessageTypeId messageTypeId, params BindingKey[] bindingKeys)
        {
            PeerId = peerId;
            SubscriptionsForType = new [] { new SubscriptionsForType(messageTypeId, bindingKeys) };
            TimestampUtc = timestampUtc;
        }

        public PeerSubscriptionsForTypesUpdated(PeerId peerId, DateTime timestampUtc, params SubscriptionsForType[] subscriptionsForType)
        {
            PeerId = peerId;
            SubscriptionsForType = subscriptionsForType;
            TimestampUtc = timestampUtc;
        }

        public override string ToString()
        {
            return $"PeerId: {PeerId}, TimestampUtc: {TimestampUtc:yyyy-MM-dd HH:mm:ss.fff}";
        }
    }
}