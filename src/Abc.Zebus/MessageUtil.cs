using System;
using Abc.Zebus.Util;

namespace Abc.Zebus
{
    public static class MessageUtil
    {
        public static MessageTypeId TypeId<T>()
            where T : IMessage
            => GetTypeId(typeof(T));

        public static MessageTypeId TypeId(this IMessage message)
            => GetTypeId(message.GetType());

        public static MessageTypeId GetTypeId(Type messageType)
            => new MessageTypeId(messageType);

        public static MessageTypeId LoadTypeId(Type messageType)
            => MessageTypeId.GetMessageTypeIdBypassCache(messageType);

        /// <summary>
        /// Useful for advanced scenarios where a given type name is loaded multiple times.
        /// Makes sure the right type is used.
        /// </summary>
        public static void RegisterMessageType(Type messageType)
        {
            TypeUtil.Register(messageType);
            MessageTypeDescriptorCache.Remove(messageType);
        }
    }
}
