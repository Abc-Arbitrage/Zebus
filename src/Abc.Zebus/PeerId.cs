using System;
using Abc.Zebus.Serialization;
using Abc.Zebus.Util.Extensions;
using Newtonsoft.Json;
using ProtoBuf;

namespace Abc.Zebus
{
    [ProtoContract, JsonConverter(typeof(PeerIdConverter))]
    public struct PeerId : IEquatable<PeerId>
    {
        [ProtoMember(1, IsRequired = true)]
        private readonly string _value;

        public PeerId(string value)
        {
            _value = value;
        }

        public bool Equals(PeerId other)
        {
            return string.Equals(_value, other._value);
        }

        public override bool Equals(object obj)
        {
            return obj is PeerId && Equals((PeerId)obj);
        }

        public override int GetHashCode()
        {
            return (_value != null ? _value.GetHashCode() : 0);
        }

        public static bool operator ==(PeerId left, PeerId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PeerId left, PeerId right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return _value;
        }

        public bool IsInstanceOf(string serviceName)
        {
            var currentServiceName = _value.Qualifier();
            return StringComparer.OrdinalIgnoreCase.Equals(currentServiceName, serviceName);
        }

        public bool IsPersistence()
        {
            return IsInstanceOf("Abc.Zebus.PersistenceService");
        }
    }
}
