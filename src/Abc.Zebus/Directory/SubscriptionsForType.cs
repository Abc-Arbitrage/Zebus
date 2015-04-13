using System;
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

        protected bool Equals(SubscriptionsForType other)
        {
            return Equals(MessageTypeId, other.MessageTypeId) && BindingKeys.SequenceEqual(other.BindingKeys);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((SubscriptionsForType)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((MessageTypeId != null ? MessageTypeId.GetHashCode() : 0) * 397) ^ (BindingKeys != null ? BindingKeys.GetHashCode() : 0);
            }
        }
    }
}