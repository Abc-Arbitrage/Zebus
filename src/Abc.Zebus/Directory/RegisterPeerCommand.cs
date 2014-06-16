using ProtoBuf;

namespace Abc.Zebus.Directory
{
    [ProtoContract, Transient]
    public sealed class RegisterPeerCommand : ICommand
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly PeerDescriptor Peer;

        public RegisterPeerCommand(PeerDescriptor peer)
        {
            Peer = peer;
        }

        public override string ToString()
        {
            return string.Format("{0} TimestampUtc: {1:yyyy-MM-dd HH:mm:ss.fff}", Peer.Peer, Peer.TimestampUtc);
        }
    }
}