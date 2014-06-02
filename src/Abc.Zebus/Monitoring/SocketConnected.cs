using ProtoBuf;

namespace Abc.Zebus.Monitoring
{
    [ProtoContract]
    [MessageTypeId("efa5c38c-08c9-496a-b892-ea141f0b3a1e")]
    public sealed partial class SocketConnected : IEvent
    {
        [ProtoMember(1, IsRequired = true)] public readonly PeerId Emitter;
        [ProtoMember(2, IsRequired = true)] public readonly PeerId Receiver;
        [ProtoMember(3, IsRequired = true)] public readonly string ReceiverEndpoint;
        
        SocketConnected () {}
        public SocketConnected (PeerId emitter, PeerId receiver, string receiverEndpoint)
        {
            Emitter = emitter;
            Receiver = receiver;
            ReceiverEndpoint = receiverEndpoint;
        }
    }
}