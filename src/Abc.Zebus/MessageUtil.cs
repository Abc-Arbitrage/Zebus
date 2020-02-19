using System;

namespace Abc.Zebus
{
    public static class MessageUtil
    {
        public static MessageTypeId TypeId<T>() where T : IMessage
        {
            return GetTypeId(typeof(T));
        }

        public static MessageTypeId TypeId(this IMessage message)
        {
            return GetTypeId(message.GetType());
        }

        public static MessageTypeId GetTypeId(Type messageType)
        {
            return new MessageTypeId(messageType);
        }
    }
}
