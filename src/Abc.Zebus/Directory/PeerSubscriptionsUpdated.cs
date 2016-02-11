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

        public override string ToString() => $"PeerId: {PeerDescriptor.PeerId}, TimestampUtc: {PeerDescriptor.TimestampUtc:yyyy-MM-dd HH:mm:ss.fff}";
    }
}