using ProtoBuf;

namespace Abc.Zebus.Directory
{
    [ProtoContract, Transient]
    public sealed class PeerStarted : IEvent
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly PeerDescriptor PeerDescriptor;

        public PeerStarted(PeerDescriptor peerDescriptor)
        {
            PeerDescriptor = peerDescriptor;
        }

        public override string ToString()
        {
            return string.Format("{0} TimestampUtc: {1:yyyy-MM-dd HH:mm:ss.fff}", PeerDescriptor.Peer, PeerDescriptor.TimestampUtc);
        }
    }
}