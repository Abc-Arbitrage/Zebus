using System.Linq;
using Abc.Zebus.Routing;
using Abc.Zebus.Util.Annotations;
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
        
        [UsedImplicitly]
        private SubscriptionsForType()
        {
        }

        public Subscription[] ToSubscriptions()
        {
            return BindingKeys == null ? new Subscription[0] : BindingKeys.Select(bindingKey => new Subscription(MessageTypeId, bindingKey)).ToArray();
        }
    }
}