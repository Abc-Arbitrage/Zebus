using System;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Abc.Zebus.Routing
{
    internal class BindingKeyToken
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

        public int Position { get; }
        public string Name { get; }

        public string GetValue(IMessage message)
        {
            try
            {
                return _valueAccessorFunc(message);
            }
            catch (NullReferenceException)
            {
                throw new InvalidOperationException($"Message of type {message.GetType().Name} is not valid. Member {Name} part of the routing key at position {Position} can not be null");
            }
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