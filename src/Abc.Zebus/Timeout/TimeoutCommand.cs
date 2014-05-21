using Abc.Zebus;
using Abc.Zebus.Routing;
using ProtoBuf;

// TODO: Namespace intentionally wrong, do not fix (will be removed from the assembly)
namespace ABC.ServiceBus.Contracts
{
    [ProtoContract, Routable]
    public class TimeoutCommand : ICommand
    {
        public static MessageTypeId TypeId = new MessageTypeId(typeof(TimeoutCommand));

        [ProtoMember(1, IsRequired = true)]
        public readonly string Key;
        [ProtoMember(2, IsRequired = true)]
        public readonly string DataType;
        [ProtoMember(3, IsRequired = true)]
        public readonly byte[] Data;
        [ProtoMember(5), RoutingPosition(1)]
        public readonly string ServiceName;

        public TimeoutCommand(string key, string dataType, byte[] data, string serviceName)
        {
            Key = key;
            DataType = dataType;
            Data = data;
            ServiceName = serviceName;
        }

        public override string ToString()
        {
            return string.Format("Key: {0}", Key);
        }
    }
}