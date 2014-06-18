using Abc.Zebus.Routing;
using ProtoBuf;

namespace Abc.Zebus.Directory
{
    [ProtoContract]
    public class SubscriptionsForType
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly MessageTypeId MessageTypeId;

        [ProtoMember(2, IsRequired = true)]
        public readonly BindingKey[] BindingKeys;

        public SubscriptionsForType(MessageTypeId messageTypeId, params BindingKey[] bindingKeys)
        {
            MessageTypeId = messageTypeId;
            BindingKeys = bindingKeys;
        }

        public SubscriptionsForType(){}
    }
}