using ProtoBuf;

namespace Abc.Zebus.Monitoring
{
    [ProtoContract]
    public sealed class SocketDisconnected : IEvent
    {
        [ProtoMember(1, IsRequired = true)] public readonly PeerId Emitter;
        [ProtoMember(2, IsRequired = true)] public readonly PeerId Receiver;
        [ProtoMember(3, IsRequired = true)] public readonly string ReceiverEndpoint;
        
        SocketDisconnected () {}
        public SocketDisconnected (PeerId emitter, PeerId receiver, string receiverEndpoint)
        {
            Emitter = emitter;
            Receiver = receiver;
            ReceiverEndpoint = receiverEndpoint;
        }
    }
}