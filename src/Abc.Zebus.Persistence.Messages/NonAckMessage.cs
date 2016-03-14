using ProtoBuf;

namespace Abc.Zebus.Persistence.Messages
{
    [ProtoContract]
    public class NonAckMessage : IMessage
    {
        [ProtoMember(1, IsRequired = true)] public readonly string InstanceName;
        [ProtoMember(2, IsRequired = true)] public readonly int Count;
        
        private NonAckMessage() { }
        
        public NonAckMessage(string instanceName, int count)
        {
            InstanceName = instanceName;
            Count = count;
        }
    }
}