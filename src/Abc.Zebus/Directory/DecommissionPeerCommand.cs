using ProtoBuf;

namespace Abc.Zebus.Directory
{
    [ProtoContract]
    public class DecommissionPeerCommand : ICommand
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly PeerId PeerId;

        public DecommissionPeerCommand(PeerId peerId)
        {
            PeerId = peerId;
        }

        public override string ToString()
        {
            return PeerId.ToString();
        }
    }
}