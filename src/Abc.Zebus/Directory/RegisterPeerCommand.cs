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

        public override string ToString() => $"{Peer.Peer} TimestampUtc: {Peer.TimestampUtc:yyyy-MM-dd HH:mm:ss.fff}";
    }
}