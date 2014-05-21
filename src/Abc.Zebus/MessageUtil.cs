using System;
using System.Collections.Concurrent;

namespace Abc.Zebus
{
    public static class MessageUtil
    {
        private static readonly ConcurrentDictionary<MessageTypeId, MessageTypeEntry> _cache = new ConcurrentDictionary<MessageTypeId, MessageTypeEntry>();
        private static readonly ConcurrentDictionary<Type, MessageTypeId> _messageTypeIds = new ConcurrentDictionary<Type, MessageTypeId>();

        private static readonly Func<MessageTypeId, MessageTypeEntry> _messageTypeEntryFactory = LoadMessageTypeEntry;
        private static readonly Func<Type, MessageTypeId> _messageTypeIdFactory = CreateMessageTypeId;

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
            return _messageTypeIds.GetOrAdd(messageType, _messageTypeIdFactory);
        }

        public static bool IsPersistent(MessageTypeId messageTypeId)
        {
            var entry = GetMessageTypeEntry(messageTypeId);
            return entry.IsPersistent;
        }

        public static bool IsInfrastructure(MessageTypeId messageTypeId)
        {
            var entry = GetMessageTypeEntry(messageTypeId);
            return entry.IsInfrastructure;
        }

        private static MessageTypeEntry GetMessageTypeEntry(MessageTypeId messageTypeId)
        {
            return _cache.GetOrAdd(messageTypeId, _messageTypeEntryFactory);
        }

        internal static void SetIsPersistent(MessageTypeId messageTypeId, bool isPersistent)
        {
            _cache.GetOrAdd(messageTypeId, _messageTypeEntryFactory).IsPersistent = isPersistent;
        }

        private static MessageTypeEntry LoadMessageTypeEntry(MessageTypeId messageTypeId)
        {
            var messageType = messageTypeId.GetMessageType();
            var isPersistent = !(messageType != null && Attribute.IsDefined(messageType, typeof(TransientAttribute)));
            var isInfrastructure = messageType != null && Attribute.IsDefined(messageType, typeof(InfrastructureAttribute));

            return new MessageTypeEntry(isPersistent, isInfrastructure);
        }

        private static MessageTypeId CreateMessageTypeId(Type messageType)
        {
            return new MessageTypeId(messageType);
        }

        private class MessageTypeEntry
        {
            public bool IsPersistent;
            public readonly bool IsInfrastructure;

            public MessageTypeEntry(bool isPersistent, bool isInfrastructure)
            {
                IsPersistent = isPersistent;
                IsInfrastructure = isInfrastructure;
            }
        }
    }
}