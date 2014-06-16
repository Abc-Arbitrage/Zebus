using ProtoBuf;

namespace Abc.Zebus.Directory
{
    [ProtoContract, Transient]
    public sealed class PeerSubscriptionsUpdated : IEvent
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly PeerDescriptor PeerDescriptor;

        public PeerSubscriptionsUpdated(PeerDescriptor peerDescriptor)
        {
            PeerDescriptor = peerDescriptor;
        }

        public override string ToString()
        {
            return string.Format("PeerId: {0}, TimestampUtc: {1:yyyy-MM-dd HH:mm:ss.fff}", PeerDescriptor.PeerId, PeerDescriptor.TimestampUtc);
        }
    }
}