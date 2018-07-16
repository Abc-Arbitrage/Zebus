using ProtoBuf;

namespace Abc.Zebus.Persistence.Messages
{
    [ProtoContract]
    [MessageTypeId("c627ca6f-9913-4db5-9bbe-604d2ee7b6f0")]
    public class PurgeMessageQueueCommand : ICommand
    {
        [ProtoMember(1, IsRequired = true)] public readonly string InstanceName;
        
        private PurgeMessageQueueCommand() { }
        
        public PurgeMessageQueueCommand(string instanceName)
        {
            InstanceName = instanceName;
        }
    }
}