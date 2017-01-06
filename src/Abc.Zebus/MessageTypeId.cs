using System;
using ProtoBuf;

namespace Abc.Zebus
{
    [ProtoContract]
    public class MessageTypeId : IEquatable<MessageTypeId>
    {
        public static readonly MessageTypeId EndOfStream = new MessageTypeId("Abc.Zebus.Transport.EndOfStream");
        public static readonly MessageTypeId EndOfStreamAck = new MessageTypeId("Abc.Zebus.Transport.EndOfStreamAck");
        public static readonly MessageTypeId PersistenceStopping = new MessageTypeId("Abc.Zebus.PersistentTransport.PersistenceStopping");
        public static readonly MessageTypeId PersistenceStoppingAck = new MessageTypeId("Abc.Zebus.PersistentTransport.PersistenceStoppingAck");

        private MessageTypeDescriptor _descriptor;

        public MessageTypeId(Type messageType)
        {
            _descriptor = MessageUtil.GetMessageTypeDescriptor(messageType);
        }

        public MessageTypeId(string fullName)
        {
            _descriptor = MessageUtil.GetMessageTypeDescriptor(fullName);
        }

        private MessageTypeId()
        {
        }

        [ProtoMember(1, IsRequired = true)]
        public string FullName
        {
            get { return _descriptor?.FullName; }
            private set { _descriptor = MessageUtil.GetMessageTypeDescriptor(value); }
        }

        public Type GetMessageType() => _descriptor?.MessageType;

        public bool IsInfrastructure() => _descriptor != null ? _descriptor.IsInfrastructure : false;

        public bool IsPersistent() => _descriptor != null ? _descriptor.IsPersistent : true;

        public override string ToString()
        {
            var lastDotIndex = FullName.LastIndexOf('.');
            return lastDotIndex != -1 ? FullName.Substring(lastDotIndex + 1) : FullName;
        }

        public bool Equals(MessageTypeId other)
        {
            return other != null && _descriptor == other._descriptor;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as MessageTypeId);
        }

        public override int GetHashCode()
        {
            return _descriptor != null ? _descriptor.GetHashCode() : 0;
        }

        public static bool operator ==(MessageTypeId left, MessageTypeId right) => Equals(left, right);

        public static bool operator !=(MessageTypeId left, MessageTypeId right) => !Equals(left, right);
    }
}