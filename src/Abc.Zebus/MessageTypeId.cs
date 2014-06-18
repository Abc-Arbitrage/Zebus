using System;
using System.Text;
using Abc.Zebus.Util;
using ProtoBuf;

namespace Abc.Zebus
{
    [ProtoContract]
    public class MessageTypeId : IEquatable<MessageTypeId>
    {
        private Type _messageType;
        private string _fullName;

        public MessageTypeId(Type messageType)
        {
            _messageType = messageType;
            FullName = GetFullnameWithNoAssemblyOrVersion(messageType);
        }

        public MessageTypeId(string fullName)
        {
            FullName = fullName;
        }

        private MessageTypeId()
        {
        }

        [ProtoMember(1, IsRequired = true)]
        public string FullName
        {
            get { return _fullName; }
            private set { _fullName = string.Intern(value); }
        }

        public static readonly MessageTypeId EndOfStream = new MessageTypeId("Abc.Zebus.Transport.EndOfStream");
        public static readonly MessageTypeId EndOfStreamAck = new MessageTypeId("Abc.Zebus.Transport.EndOfStreamAck");
        public static readonly MessageTypeId PersistenceStopping = new MessageTypeId("Abc.Zebus.PersistentTransport.PersistenceStopping");
        public static readonly MessageTypeId PersistenceStoppingAck = new MessageTypeId("Abc.Zebus.PersistentTransport.PersistenceStoppingAck");

        public Type GetMessageType()
        {
            return _messageType ?? (_messageType = TypeUtil.Resolve(FullName));
        }

        public bool IsPersistent()
        {
            return MessageUtil.IsPersistent(this);
        }

        public bool IsInfrastructure()
        {
            return MessageUtil.IsInfrastructure(this);
        }

        public override string ToString()
        {
            var lastDotIndex = FullName.LastIndexOf('.');
            return lastDotIndex != -1 ? FullName.Substring(lastDotIndex + 1) : FullName;
        }
        
        private string GetFullnameWithNoAssemblyOrVersion(Type messageType)
        {
            if (!messageType.IsGenericType)
                return messageType.FullName;

            var genericTypeDefinition = messageType.GetGenericTypeDefinition();
            var builder = new StringBuilder();
            if (messageType.IsNested)
                builder.AppendFormat("{0}+", messageType.DeclaringType.FullName);
            else
                builder.AppendFormat("{0}.", genericTypeDefinition.Namespace);

            var backQuoteIndex = genericTypeDefinition.Name.IndexOf('`');
            builder.Append(genericTypeDefinition.Name.Substring(0, backQuoteIndex));
            builder.Append("<");
            foreach (var genericArgument in messageType.GetGenericArguments())
            {
                if (genericArgument.IsGenericType)
                    throw new InvalidOperationException("Nested generics are not supported");
                builder.AppendFormat("{0}.{1}, ", genericArgument.Namespace, genericArgument.Name);
            }
                

            builder.Length -= 2;
            builder.Append(">");
            return builder.ToString();
        }

        public bool Equals(MessageTypeId other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return string.Equals(FullName, other.FullName);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((MessageTypeId)obj);
        }

        public static bool operator ==(MessageTypeId left, MessageTypeId right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(MessageTypeId left, MessageTypeId right)
        {
            return !Equals(left, right);
        }

        public override int GetHashCode()
        {
            return (FullName != null ? FullName.GetHashCode() : 0);
        }
    }
}