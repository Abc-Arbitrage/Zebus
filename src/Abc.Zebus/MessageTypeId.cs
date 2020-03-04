using System;
using JetBrains.Annotations;
using ProtoBuf;

namespace Abc.Zebus
{
    [ProtoContract(Surrogate = typeof(ProtobufSurrogate))]
    public readonly struct MessageTypeId : IEquatable<MessageTypeId>
    {
        public static readonly MessageTypeId EndOfStream = new MessageTypeId("Abc.Zebus.Transport.EndOfStream");
        public static readonly MessageTypeId EndOfStreamAck = new MessageTypeId("Abc.Zebus.Transport.EndOfStreamAck");
        public static readonly MessageTypeId PersistenceStopping = new MessageTypeId("Abc.Zebus.PersistentTransport.PersistenceStopping");
        public static readonly MessageTypeId PersistenceStoppingAck = new MessageTypeId("Abc.Zebus.PersistentTransport.PersistenceStoppingAck");

        private readonly MessageTypeDescriptor? _descriptor;

        public MessageTypeId(Type? messageType)
            => _descriptor = MessageTypeDescriptorCache.GetMessageTypeDescriptor(messageType);

        public MessageTypeId(string? fullName)
            => _descriptor = MessageTypeDescriptorCache.GetMessageTypeDescriptor(fullName);

        private MessageTypeId(MessageTypeDescriptor descriptor)
            => _descriptor = descriptor;

        public string? FullName => Descriptor.FullName;

        [System.Diagnostics.Contracts.Pure]
        public Type? GetMessageType() => Descriptor.MessageType;

        [System.Diagnostics.Contracts.Pure]
        public bool IsInfrastructure() => Descriptor.IsInfrastructure;

        [System.Diagnostics.Contracts.Pure]
        public bool IsPersistent() => Descriptor.IsPersistent;

        internal MessageTypeDescriptor Descriptor => _descriptor ?? MessageTypeDescriptor.Null;

        public override string ToString()
        {
            if (FullName is null)
                return "(unknown type)";

            var lastDotIndex = FullName.LastIndexOf('.');
            return lastDotIndex != -1 ? FullName.Substring(lastDotIndex + 1) : FullName;
        }

        internal static MessageTypeId GetMessageTypeIdBypassCache(Type? messageType)
            => new MessageTypeId(MessageTypeDescriptorCache.GetMessageTypeDescriptorBypassCache(messageType));

        public bool Equals(MessageTypeId other) => _descriptor == other._descriptor;
        public override bool Equals(object? obj) => obj is MessageTypeId messageTypeId && Equals(messageTypeId);

        public override int GetHashCode() => Descriptor.GetHashCode();

        public static bool operator ==(MessageTypeId left, MessageTypeId right) => left.Equals(right);
        public static bool operator !=(MessageTypeId left, MessageTypeId right) => !left.Equals(right);

        [ProtoContract]
        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        internal struct ProtobufSurrogate
        {
            [ProtoMember(1, IsRequired = true)]
            private string? FullName { get; set; }

            private ProtobufSurrogate(MessageTypeId typeId)
                => FullName = typeId.FullName;

            public static implicit operator MessageTypeId(ProtobufSurrogate value) => new MessageTypeId(value.FullName);
            public static implicit operator ProtobufSurrogate(MessageTypeId value) => new ProtobufSurrogate(value);
        }
    }
}
