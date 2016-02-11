using System;
using Abc.Zebus.Routing;

namespace Abc.Zebus.Directory
{
    public struct MessageBinding : IEquatable<MessageBinding>
    {
        public readonly MessageTypeId MessageTypeId;
        public readonly BindingKey RoutingKey;

        public MessageBinding(MessageTypeId messageTypeId, BindingKey routingKey)
        {
            MessageTypeId = messageTypeId;
            RoutingKey = routingKey;
        }

        public static MessageBinding FromMessage(IMessage message)
        {
            return new MessageBinding(message.TypeId(), BindingKey.Create(message));
        }

        public static MessageBinding Default<T>() where T : IMessage
        {
            return new MessageBinding(MessageUtil.TypeId<T>(), BindingKey.Empty);
        }

        public bool Equals(MessageBinding other)
        {
            return Equals(MessageTypeId, other.MessageTypeId) && RoutingKey.Equals(other.RoutingKey);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            return obj is MessageBinding && Equals((MessageBinding)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((MessageTypeId?.GetHashCode() ?? 0) * 397) ^ RoutingKey.GetHashCode();
            }
        }
    }
}