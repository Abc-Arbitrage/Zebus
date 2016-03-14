using ProtoBuf;

namespace Abc.Zebus.Persistence.Messages
{
    [ProtoContract]
    [MessageTypeId("a60ad98e-dcc6-4688-949d-0584603e853a")]
    public class DeleteBucketCommand : ICommand
    {
        [ProtoMember(1, IsRequired = true)] public readonly string BucketName;
        [ProtoMember(2, IsRequired = true)] public readonly string InstanceName;
        
        private DeleteBucketCommand() { }
        
        public DeleteBucketCommand(string bucketName, string instanceName)
        {
            BucketName = bucketName;
            InstanceName = instanceName;
        }
    }
}