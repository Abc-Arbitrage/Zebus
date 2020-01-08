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
        private const string _star = "*";
        private const string _sharp = "#";
        public static readonly BindingKey Empty = new BindingKey();

        private static readonly ConcurrentDictionary<Type, BindingKeyBuilder?> _builders = new ConcurrentDictionary<Type, BindingKeyBuilder?>();
        private static readonly Func<Type, BindingKeyBuilder?> _bindingKeyBuilderFactory = CreateBuilder;

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
            => _parts?[index] == _sharp;

        public bool IsStar(int index)
            => _parts?[index] == _star;

        [Pure]
        public string? GetPart(int index)
            => index < PartCount ? _parts![index] : null;

        [Pure]
        public IEnumerable<string> GetParts()
            => _parts?.ToList() ?? Enumerable.Empty<string>();

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
                return _sharp;

            return string.Join(".", _parts);
        }

        internal static BindingKey Create(IMessage message)
            => GetBindingKeyBuilder(message.GetType())?.BuildKey(message) ?? Empty;

        internal static BindingKey Create(Type messageType, IDictionary<string, string> fieldValues)
            => GetBindingKeyBuilder(messageType)?.BuildKey(fieldValues) ?? Empty;

        private static BindingKeyBuilder? GetBindingKeyBuilder(Type messageType)
            => _builders.GetOrAdd(messageType, _bindingKeyBuilderFactory);

        private static BindingKeyBuilder? CreateBuilder(Type messageType)
        {
            if (!Attribute.IsDefined(messageType, typeof(Routable)))
                return null;

            return new BindingKeyBuilder(messageType);
        }

        private class BindingKeyBuilder
        {
            private readonly BindingKeyToken[] _tokens;

            public BindingKeyBuilder(Type messageType)
            {
                var fields = from field in messageType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                             let attributes = field.GetCustomAttributes(typeof(RoutingPositionAttribute), true)
                             where attributes.Length == 1
                             select new BindingKeyToken(((RoutingPositionAttribute)attributes[0]).Position, messageType, field);

                var properties = from property in messageType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                 let attributes = property.GetCustomAttributes(typeof(RoutingPositionAttribute), true)
                                 where attributes.Length == 1
                                 select new BindingKeyToken(((RoutingPositionAttribute)attributes[0]).Position, messageType, property);

                _tokens = fields.Concat(properties).OrderBy(x => x.Position).ToArray();
            }

            public BindingKey BuildKey(IMessage message)
            {
                var parts = new string[_tokens.Length];
                for (var tokenIndex = 0; tokenIndex < parts.Length; ++tokenIndex)
                {
                    parts[tokenIndex] = _tokens[tokenIndex].GetValue(message);
                }

                return new BindingKey(parts);
            }

            public BindingKey BuildKey(IDictionary<string, string> fieldValues)
            {
                var parts = new string[_tokens.Length];
                for (var tokenIndex = 0; tokenIndex < _tokens.Length; ++tokenIndex)
                {
                    parts[tokenIndex] = fieldValues.GetValueOrDefault(_tokens[tokenIndex].Name, _star);
                }

                return new BindingKey(parts);
            }
        }
    }
}
