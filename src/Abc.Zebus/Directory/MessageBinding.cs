using System;
using Abc.Zebus.Routing;

namespace Abc.Zebus.Directory
{
    public readonly struct MessageBinding : IEquatable<MessageBinding>
    {
        public readonly MessageTypeId MessageTypeId;
        public readonly BindingKey RoutingKey;

        public MessageBinding(MessageTypeId messageTypeId, BindingKey routingKey)
        {
            MessageTypeId = messageTypeId;
            RoutingKey = routingKey;
        }

        public static MessageBinding FromMessage(IMessage message)
            => new MessageBinding(message.TypeId(), BindingKey.Create(message));

        public static MessageBinding Default<T>()
            where T : IMessage
            => new MessageBinding(MessageUtil.TypeId<T>(), BindingKey.Empty);

        public bool Equals(MessageBinding other)
            => MessageTypeId == other.MessageTypeId && RoutingKey.Equals(other.RoutingKey);

        public override bool Equals(object? obj)
            => obj is MessageBinding binding && Equals(binding);

        public override int GetHashCode()
        {
            unchecked
            {
                return (MessageTypeId.GetHashCode() * 397) ^ RoutingKey.GetHashCode();
            }
        }
    }
}
