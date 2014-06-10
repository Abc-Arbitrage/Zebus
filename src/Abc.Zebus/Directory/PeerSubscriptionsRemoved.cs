using System;
using ProtoBuf;

namespace Abc.Zebus.Directory
{
    [ProtoContract, Transient]
    public sealed class PeerSubscriptionsRemoved: IEvent
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly PeerId PeerId;

        [ProtoMember(2, IsRequired = true)]
        public readonly Subscription[] Subscriptions;

        [ProtoMember(3, IsRequired = true)]
        public readonly DateTime TimestampUtc;

        public PeerSubscriptionsRemoved(PeerId peerId, Subscription[] subscriptions, DateTime timestampUtc)
        {
            PeerId = peerId;
            Subscriptions = subscriptions;
            TimestampUtc = timestampUtc;
        }

        public override string ToString()
        {
            return string.Format("PeerId: {0}, Subscriptions: {1}", PeerId.ToString(), Subscriptions != null ? Subscriptions.Length : 0);
        }
    }
}