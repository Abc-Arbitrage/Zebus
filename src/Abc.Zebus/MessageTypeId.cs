using System;
using System.Diagnostics.CodeAnalysis;
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

        private readonly MessageTypeDescriptor _descriptor;

        public MessageTypeId(Type messageType)
        {
            _descriptor = MessageUtil.GetMessageTypeDescriptor(messageType);
        }

        public MessageTypeId(string fullName)
        {
            _descriptor = MessageUtil.GetMessageTypeDescriptor(fullName);
        }

        public string FullName => _descriptor?.FullName;

        [System.Diagnostics.Contracts.Pure]
        public Type GetMessageType() => _descriptor?.MessageType;

        [System.Diagnostics.Contracts.Pure]
        public bool IsInfrastructure() => _descriptor?.IsInfrastructure ?? false;

        [System.Diagnostics.Contracts.Pure]
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

        [ProtoContract]
        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        internal struct ProtobufSurrogate
        {
            [ProtoMember(1, IsRequired = true)]
            private string FullName { get; set; }

            private ProtobufSurrogate(MessageTypeId typeId)
            {
                FullName = typeId.FullName;
            }

            public static implicit operator MessageTypeId(ProtobufSurrogate value) => new MessageTypeId(value.FullName);
            public static implicit operator ProtobufSurrogate(MessageTypeId value) => new ProtobufSurrogate(value);
        }
    }
}
