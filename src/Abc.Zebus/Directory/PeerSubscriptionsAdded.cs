using System;
using ProtoBuf;

namespace Abc.Zebus.Directory
{
    [ProtoContract, Transient]
    public sealed class PeerSubscriptionsAdded: IEvent
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly PeerId PeerId;

        [ProtoMember(2, IsRequired = true)]
        public readonly Subscription[] Subscriptions;

        [ProtoMember(3, IsRequired = true)]
        public readonly DateTime TimestampUtc;

        public PeerSubscriptionsAdded(PeerId peerId, Subscription[] subscriptions, DateTime timestampUtc)
        {
            PeerId = peerId;
            Subscriptions = subscriptions;
            TimestampUtc = timestampUtc;
        }

        public override string ToString()
        {
            return string.Format("PeerId: {0}, Subscriptions: {1}, TimestampUtc: {2:yyyy-MM-dd HH:mm:ss.fff}", PeerId, Subscriptions != null ? Subscriptions.Length : 0, TimestampUtc);
        }
    }
}