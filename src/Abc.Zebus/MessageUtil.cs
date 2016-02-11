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

        public static bool IsMessageMarkedAsPersistent(MessageTypeId messageTypeId)
        {
            var entry = GetMessageTypeEntry(messageTypeId);
            return entry.IsMarkedAsPersistent;
        }

        public static bool IsInfrastructure(MessageTypeId messageTypeId)
        {
            var entry = GetMessageTypeEntry(messageTypeId);
            return entry.IsInfrastructureMessage;
        }

        private static MessageTypeEntry GetMessageTypeEntry(MessageTypeId messageTypeId)
        {
            return _cache.GetOrAdd(messageTypeId, _messageTypeEntryFactory);
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
            public readonly bool IsMarkedAsPersistent;
            public readonly bool IsInfrastructureMessage;

            public MessageTypeEntry(bool isMarkedAsPersistent, bool isInfrastructureMessage)
            {
                IsMarkedAsPersistent = isMarkedAsPersistent;
                IsInfrastructureMessage = isInfrastructureMessage;
            }
        }
    }
}
