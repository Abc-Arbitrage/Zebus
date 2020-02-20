using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Abc.Zebus.Util.Extensions;
using JetBrains.Annotations;
using ProtoBuf;

namespace Abc.Zebus.Routing
{
    [ProtoContract]
    public readonly struct BindingKey : IEquatable<BindingKey>
    {
        public static readonly BindingKey Empty = new BindingKey();

        [ProtoMember(1, IsRequired = true)]
        private readonly string[]? _parts;

        public BindingKey(params string[] parts)
        {
            if (parts == null || parts.Length == 0)
                _parts = null;
            else
                _parts = parts;
        }

        public int PartCount => _parts?.Length ?? 0;

        public bool IsEmpty => _parts == null || _parts.Length == 1 && IsSharp(0);

        public bool IsSharp(int index)
            => _parts?[index] == BindingKeyPart.SharpToken;

        public bool IsStar(int index)
            => _parts?[index] == BindingKeyPart.StarToken;

        [Pure]
        public string? GetPartToken(int index)
            => index < PartCount ? _parts![index] : null;

        [Pure]
        public BindingKeyPart GetPart(int index)
        {
            if (_parts == null)
                return BindingKeyPart.Star;

            if (index < _parts.Length)
                return BindingKeyPart.Parse(_parts[index]);

            for (var i = 0; i < _parts.Length; i++)
            {
                if (IsSharp(i))
                    return BindingKeyPart.Star;
            }

            return BindingKeyPart.Null;
        }

        public override bool Equals(object? obj)
            => obj is BindingKey other && Equals(other);

        public bool Equals(BindingKey other)
        {
            if (Equals(_parts, other._parts))
                return true;

            if (_parts == null || other._parts == null || _parts.Length != other._parts.Length)
                return false;

            for (var partIndex = 0; partIndex < _parts.Length; ++partIndex)
            {
                if (!string.Equals(_parts[partIndex], other._parts[partIndex]))
                    return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            if (_parts == null || _parts.Length == 0)
                return 0;

            var hashCode = _parts[0].GetHashCode();
            for (var partIndex = 1; partIndex < _parts.Length; ++partIndex)
            {
                hashCode = (hashCode * 397) ^ _parts[partIndex].GetHashCode();
            }

            return hashCode;
        }

        public override string ToString()
        {
            if (_parts == null)
                return BindingKeyPart.SharpToken;

            return string.Join(".", _parts);
        }

        internal static BindingKey Create(IMessage message)
        {
            var routingMembers = message.TypeId().Descriptor.RoutingMembers;
            if (routingMembers.Length == 0)
                return Empty;

            var parts = new string[routingMembers.Length];

            for (var tokenIndex = 0; tokenIndex < parts.Length; ++tokenIndex)
            {
                parts[tokenIndex] = routingMembers[tokenIndex].GetValue(message);
            }

            return new BindingKey(parts);
        }

        internal static BindingKey Create(Type messageType, IDictionary<string, string> fieldValues)
        {
            var routingMembers = MessageUtil.GetTypeId(messageType).Descriptor.RoutingMembers;
            if (routingMembers.Length == 0)
                return Empty;

            var parts = new string[routingMembers.Length];
            for (var tokenIndex = 0; tokenIndex < routingMembers.Length; ++tokenIndex)
            {
                parts[tokenIndex] = fieldValues.GetValueOrDefault(routingMembers[tokenIndex].Member.Name, BindingKeyPart.StarToken);
            }

            return new BindingKey(parts);
        }
    }
}
