using System;
using Abc.Zebus.Serialization;
using Abc.Zebus.Util.Extensions;
using Newtonsoft.Json;
using ProtoBuf;

namespace Abc.Zebus
{
    [ProtoContract, JsonConverter(typeof(PeerIdConverter))]
    public readonly struct PeerId : IEquatable<PeerId>
    {
        [ProtoMember(1, IsRequired = true)]
        private readonly string _value;

        public PeerId(string value)
        {
            _value = value;
        }

        public bool Equals(PeerId other) => string.Equals(_value, other._value);
        public override bool Equals(object obj) => obj is PeerId && Equals((PeerId)obj);

        public override int GetHashCode() => _value?.GetHashCode() ?? 0;

        public static bool operator ==(PeerId left, PeerId right) => left.Equals(right);
        public static bool operator !=(PeerId left, PeerId right) => !left.Equals(right);

        public override string ToString() => _value ?? string.Empty;

        public bool IsInstanceOf(string serviceName)
            => StringComparer.OrdinalIgnoreCase.Equals(_value.Qualifier(), serviceName);

        public bool IsPersistence() => IsInstanceOf("Abc.Zebus.PersistenceService");
    }
}
