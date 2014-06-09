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

        public PeerSubscriptionsRemoved(PeerId peerId, Subscription[] subscriptions)
        {
            PeerId = peerId;
            Subscriptions = subscriptions;
        }

        public override string ToString()
        {
            return PeerId.ToString();
        }
    }
}