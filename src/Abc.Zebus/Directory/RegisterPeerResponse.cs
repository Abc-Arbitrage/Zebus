using ProtoBuf;

namespace Abc.Zebus.Directory;

[ProtoContract]
public class RegisterPeerResponse : IMessage
{
    [ProtoMember(1, IsRequired = true)]
    public readonly PeerDescriptor[] PeerDescriptors;

    public RegisterPeerResponse(PeerDescriptor[] peerDescriptors)
    {
        PeerDescriptors = peerDescriptors;
    }
}
