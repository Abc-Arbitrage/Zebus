using System;
using ProtoBuf;

namespace Abc.Zebus.Directory
{
    [ProtoContract, Transient]
    public class AddPeerSubscriptionsCommand : ICommand
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly PeerId PeerId;

        [ProtoMember(2, IsRequired = true)]
        public readonly Subscription[] Subscriptions;

        [ProtoMember(3, IsRequired = false)]
        public readonly DateTime TimestampUtc;

        public AddPeerSubscriptionsCommand(PeerId peerId, Subscription[] subscriptions, DateTime timestampUtc)
        {
            PeerId = peerId;
            Subscriptions = subscriptions;
            TimestampUtc = timestampUtc;
        }

        public override string ToString()
        {
            return string.Format("{0} TimestampUtc: {1:yyyy-MM-dd HH:mm:ss.fff}", PeerId, TimestampUtc);
        }
    }
}