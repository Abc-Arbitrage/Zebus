using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Abc.Zebus.Util.Annotations;
using Abc.Zebus.Util.Extensions;
using ProtoBuf;

namespace Abc.Zebus.Routing
{
    [ProtoContract]
    public struct BindingKey : IEquatable<BindingKey>
    {
        public static readonly BindingKey Empty = new BindingKey();

        private static readonly ConcurrentDictionary<Type, BindingKeyBuilder> _builders = new ConcurrentDictionary<Type, BindingKeyBuilder>();
        private static readonly Func<Type, BindingKeyBuilder> _bindingKeyBuilderFactory = CreateBuilder;
        private static readonly char[] _separator = { '.' };

        [ProtoMember(1, IsRequired = true)]
        private readonly string[] _parts;

        private readonly bool _isJoined;

        public BindingKey(params string[] parts) : this(parts, false)
        {
        }

        private BindingKey(string[] parts, bool isJoined)
        {
            if (parts == null || parts.Length == 0)
                _parts = null;
            else
                _parts = parts;

            _isJoined = isJoined;
        }

        public int PartCount
        {
            get { return _parts != null ? _parts.Length : 0; }
        }

        public bool IsEmpty
        {
            get { return _parts == null; }
        }

        internal bool IsJoined
        {
            get { return _isJoined; }
        }

        [Pure]
        public string GetPart(int index)
        {
            return index < PartCount ? _parts[index] : null;
        }

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
                return "#";

            return string.Join(".", _parts);
        }

        /// <summary>
        /// For Qpid compatibility only, do not use in Zebus code.
        /// </summary>
        internal static BindingKey Joined(string s)
        {
            return new BindingKey(new [] { s }, true);
        }

        internal static BindingKey Split(string s)
        {
            return new BindingKey(s.Split(_separator));
        }

        internal static BindingKey Create(IMessage message)
        {
            var builder = GetBindingKeyBuilder(message.GetType());
            return builder != null ? builder.BuildKey(message) : Empty;
        }

        internal static BindingKey Create(Type messageType, IDictionary<string, string> fieldValues)
        {
            var builder = GetBindingKeyBuilder(messageType);
            return builder != null ? builder.BuildKey(fieldValues) : Empty;
        }

        private static BindingKeyBuilder GetBindingKeyBuilder(Type messageType)
        {
            return _builders.GetOrAdd(messageType, _bindingKeyBuilderFactory);
        }

        private static BindingKeyBuilder CreateBuilder(Type messageType)
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
                    parts[tokenIndex] = fieldValues.GetValueOrDefault(_tokens[tokenIndex].Name, "*");
                }
                return new BindingKey(parts);
            }
        }

        private class BindingKeyToken
        {
            private static readonly MethodInfo _toStringMethod = typeof(object).GetMethod("ToString");
            private static readonly MethodInfo _toStringWithFormatMethod = typeof(IConvertible).GetMethod("ToString");
            private readonly Func<IMessage, string> _valueAccessorFunc;

            public BindingKeyToken(int position, Type messageType, FieldInfo fieldInfo)
            {
                Position = position;
                Name = fieldInfo.Name;

                Func<Expression, Expression> fieldValueAccessor = m => Expression.Field(m, fieldInfo);
                _valueAccessorFunc = GenerateValueAccessor(fieldValueAccessor, messageType, fieldInfo.FieldType);
            }

            public BindingKeyToken(int position, Type messageType, PropertyInfo propertyInfo)
            {
                Position = position;
                Name = propertyInfo.Name;

                Func<Expression, Expression> propertyValueAccessor = m => Expression.Property(m, propertyInfo);
                _valueAccessorFunc = GenerateValueAccessor(propertyValueAccessor, messageType, propertyInfo.PropertyType);
            }

            public int Position { get; private set; }
            public string Name { get; private set; }

            public string GetValue(IMessage message)
            {
                return _valueAccessorFunc(message);
            }

            private Func<IMessage, string> GenerateValueAccessor(Func<Expression, Expression> valueAccessor, Type messageType, Type memberType)
            {
                var message = Expression.Parameter(typeof(IMessage), "m");
                var castedMessage = Expression.Convert(message, messageType);

                var body = typeof(IConvertible).IsAssignableFrom(memberType) && memberType != typeof(string)
                               ? Expression.Call(valueAccessor(castedMessage), _toStringWithFormatMethod, Expression.Constant(CultureInfo.InvariantCulture))
                               : Expression.Call(valueAccessor(castedMessage), _toStringMethod);

                var lambda = Expression.Lambda(typeof(Func<IMessage, string>), body, message);
                return (Func<IMessage, string>)lambda.Compile();
            }
        }
    }
}