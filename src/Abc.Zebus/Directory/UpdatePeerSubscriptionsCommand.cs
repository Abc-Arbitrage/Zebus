using System;
using System.Linq;
using ProtoBuf;

namespace Abc.Zebus.Directory
{
    [ProtoContract, Transient]
    public class UpdatePeerSubscriptionsCommand : ICommand
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly PeerId PeerId;

        [ProtoMember(2, IsRequired = true)]
        public readonly Subscription[] Subscriptions;

        [ProtoMember(3, IsRequired = false)]
        public readonly DateTime? TimestampUtc;

        public UpdatePeerSubscriptionsCommand(PeerId peerId, Subscription[] subscriptions, DateTime logicalClock)
        {
            PeerId = peerId;
            Subscriptions = subscriptions;
            TimestampUtc = logicalClock;
        }

        public override string ToString()
        {
            return string.Format("PeerId: {0}, TimestampUtc: {1:yyyy-MM-dd HH:mm:ss.fff}, Subscriptions: [{2}]", PeerId, TimestampUtc, string.Join(", ", Subscriptions.AsEnumerable()));
        }
    }
}