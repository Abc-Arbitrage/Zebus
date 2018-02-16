using System;
using System.Diagnostics.CodeAnalysis;
using ProtoBuf;

namespace Abc.Zebus
{
    [ProtoContract]
    public struct MessageTypeId : IEquatable<MessageTypeId>
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

        [ProtoMember(1, IsRequired = true)]
        public string FullName
        {
            get => _descriptor?.FullName;
            private set => _descriptor = MessageUtil.GetMessageTypeDescriptor(value);
        }

        public Type GetMessageType() => _descriptor?.MessageType;
        public bool IsInfrastructure() => _descriptor?.IsInfrastructure ?? false;
        public bool IsPersistent() => _descriptor?.IsPersistent ?? true;

        public override string ToString()
        {
            var lastDotIndex = FullName.LastIndexOf('.');
            return lastDotIndex != -1 ? FullName.Substring(lastDotIndex + 1) : FullName;
        }

        public bool Equals(MessageTypeId other) => _descriptor == other._descriptor;
        public override bool Equals(object obj) => obj is MessageTypeId messageTypeId && Equals(messageTypeId);

        [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
        public override int GetHashCode() => _descriptor?.GetHashCode() ?? 0;

        public static bool operator ==(MessageTypeId left, MessageTypeId right) => left.Equals(right);
        public static bool operator !=(MessageTypeId left, MessageTypeId right) => !left.Equals(right);
    }
}
