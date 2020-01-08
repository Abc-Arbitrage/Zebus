using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Routing;
using JetBrains.Annotations;
using ProtoBuf;

namespace Abc.Zebus.Directory
{
    [ProtoContract]
    public class SubscriptionsForType : IEquatable<SubscriptionsForType>
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly MessageTypeId MessageTypeId;

        [ProtoMember(2, IsRequired = true)]
        public readonly BindingKey[] BindingKeys = Array.Empty<BindingKey>();

        public SubscriptionsForType(MessageTypeId messageTypeId, params BindingKey[] bindingKeys)
        {
            MessageTypeId = messageTypeId;
            BindingKeys = bindingKeys;
        }

        [UsedImplicitly]
        private SubscriptionsForType()
        {
        }

        public static SubscriptionsForType Create<TMessage>(params BindingKey[] bindingKeys)
            where TMessage : IMessage
            => new SubscriptionsForType(MessageUtil.TypeId<TMessage>(), bindingKeys);

        public static Dictionary<MessageTypeId, SubscriptionsForType> CreateDictionary(IEnumerable<Subscription> subscriptions)
            => subscriptions.GroupBy(sub => sub.MessageTypeId)
                            .ToDictionary(grp => grp.Key, grp => new SubscriptionsForType(grp.Key, grp.Select(sub => sub.BindingKey).ToArray()));

        public Subscription[] ToSubscriptions()
            => BindingKeys?.Select(bindingKey => new Subscription(MessageTypeId, bindingKey)).ToArray() ?? new Subscription[0];

        public List<SubscriptionDefinition> GetDefinition() => BindingKeys.Select(x => new SubscriptionDefinition(MessageTypeId.GetMessageType(), x)).ToList();

        public bool Equals(SubscriptionsForType other)
            => other != null && MessageTypeId == other.MessageTypeId && BindingKeys.SequenceEqual(other.BindingKeys);

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != GetType())
                return false;
            return Equals((SubscriptionsForType)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (MessageTypeId.GetHashCode() * 397) ^ (BindingKeys?.GetHashCode() ?? 0);
            }
        }
    }
}
