using System;
using Abc.Zebus.Util;

namespace Abc.Zebus
{
    internal class MessageTypeDescriptor
    {
        public static readonly MessageTypeDescriptor Null = new MessageTypeDescriptor(null!, null, false, false); // TODO replace with null

        private MessageTypeDescriptor(string fullName, Type? messageType, bool isPersistent, bool isInfrastructure)
        {
            FullName = fullName;
            MessageType = messageType;
            IsPersistent = isPersistent;
            IsInfrastructure = isInfrastructure;
        }

        public string FullName { get; }
        public Type? MessageType { get; }
        public bool IsPersistent { get; }
        public bool IsInfrastructure { get; }

        public static MessageTypeDescriptor Load(string fullName)
        {
            var messageType = TypeUtil.Resolve(fullName);
            var isPersistent = messageType == null || !Attribute.IsDefined(messageType, typeof(TransientAttribute));
            var isInfrastructure = messageType != null && Attribute.IsDefined(messageType, typeof(InfrastructureAttribute));

            return new MessageTypeDescriptor(fullName, messageType, isPersistent, isInfrastructure);
        }
    }
}
