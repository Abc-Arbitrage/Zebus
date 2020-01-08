using System;
using System.Collections.Concurrent;
using Abc.Zebus.Util;

namespace Abc.Zebus
{
    public static class MessageUtil
    {
        private static readonly ConcurrentDictionary<string, MessageTypeDescriptor> _descriptorsByFullName = new ConcurrentDictionary<string, MessageTypeDescriptor>();
        private static readonly ConcurrentDictionary<Type, MessageTypeDescriptor> _descriptorsByType = new ConcurrentDictionary<Type, MessageTypeDescriptor>();

        private static readonly Func<string, MessageTypeDescriptor> _loadDescriptorFromName = LoadMessageTypeDescriptor;
        private static readonly Func<Type, MessageTypeDescriptor> _loadDescriptorFromType = LoadMessageTypeDescriptor;

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

        internal static MessageTypeDescriptor GetMessageTypeDescriptor(string? fullName)
        {
            if (fullName == null)
                return MessageTypeDescriptor.Null;

            return _descriptorsByFullName.GetOrAdd(fullName, _loadDescriptorFromName);
        }

        private static MessageTypeDescriptor LoadMessageTypeDescriptor(string fullName)
        {
            return MessageTypeDescriptor.Load(fullName);
        }

        internal static MessageTypeDescriptor GetMessageTypeDescriptor(Type? messageType)
        {
            if (messageType == null)
                return MessageTypeDescriptor.Null;

            return _descriptorsByType.GetOrAdd(messageType, _loadDescriptorFromType);
        }

        private static MessageTypeDescriptor LoadMessageTypeDescriptor(Type messageType)
        {
            var fullName = TypeUtil.GetFullnameWithNoAssemblyOrVersion(messageType);
            return GetMessageTypeDescriptor(fullName);
        }
    }
}
