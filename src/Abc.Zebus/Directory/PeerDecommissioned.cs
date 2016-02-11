using ProtoBuf;

namespace Abc.Zebus.Directory
{
    [ProtoContract, Transient]
    public class PeerDecommissioned : IEvent
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly PeerId PeerId;

        public PeerDecommissioned(PeerId peerId)
        {
            PeerId = peerId;
        }

        public override string ToString() => PeerId.ToString();
    }
}