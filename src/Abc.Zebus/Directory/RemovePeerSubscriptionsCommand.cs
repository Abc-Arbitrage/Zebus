using System;
using ProtoBuf;

namespace Abc.Zebus.Directory
{
    [ProtoContract, Transient]
    public class RemovePeerSubscriptionsCommand: ICommand
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly PeerId PeerId;

        [ProtoMember(2, IsRequired = true)]
        public readonly Subscription[] Subscriptions;

        [ProtoMember(3, IsRequired = false)]
        public readonly DateTime? TimestampUtc;

        public RemovePeerSubscriptionsCommand(PeerId peerId, Subscription[] subscriptions, DateTime logicalClock)
        {
            PeerId = peerId;
            Subscriptions = subscriptions;
            TimestampUtc = logicalClock;
        }

        public override string ToString()
        {
            return PeerId.ToString();
        }
    }
}