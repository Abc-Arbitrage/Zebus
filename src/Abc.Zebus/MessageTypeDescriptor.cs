using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Abc.Zebus.Routing;
using Abc.Zebus.Util;

namespace Abc.Zebus
{
    internal class MessageTypeDescriptor
    {
        public static readonly MessageTypeDescriptor Null = new MessageTypeDescriptor(null!);

        private MessageTypeDescriptor(string fullName, Type? messageType = null, bool isPersistent = true, bool isInfrastructure = false, RoutingMember[]? routingMembers = null)
        {
            FullName = fullName;
            MessageType = messageType;
            IsPersistent = isPersistent;
            IsInfrastructure = isInfrastructure;
            RoutingMembers = routingMembers ?? Array.Empty<RoutingMember>();
        }

        public string FullName { get; }
        public Type? MessageType { get; }
        public bool IsPersistent { get; }
        public bool IsInfrastructure { get; }
        public RoutingMember[] RoutingMembers { get; }

        public bool TryGetRoutingMemberByName(string memberName, [MaybeNullWhen(false)] out RoutingMember routingMember)
        {
            foreach (var member in RoutingMembers)
            {
                if (member.Member.Name == memberName)
                {
                    routingMember = member;
                    return true;
                }
            }

            routingMember = default!;
            return false;
        }

        public static MessageTypeDescriptor Load(string? fullName)
        {
            if (fullName == null)
                return Null;

            return Load(TypeUtil.Resolve(fullName), fullName);
        }

        public static MessageTypeDescriptor Load(Type? messageType, string? fullName)
        {
            if (fullName == null)
                return Null;

            if (messageType == null)
                return new MessageTypeDescriptor(fullName);

            var isPersistent = !Attribute.IsDefined(messageType, typeof(TransientAttribute));
            var isInfrastructure = Attribute.IsDefined(messageType, typeof(InfrastructureAttribute));
            var routingMembers = RoutingMember.GetAll(messageType);

            return new MessageTypeDescriptor(fullName, messageType, isPersistent, isInfrastructure, routingMembers);
        }

        public class RoutingMember
        {
            private static readonly MethodInfo _getValueMethod = typeof(RoutingMember).GetMethod(nameof(GetMemberValue), BindingFlags.Static | BindingFlags.NonPublic)!;
            private static readonly MethodInfo _getValueConvertibleMethod = typeof(RoutingMember).GetMethod(nameof(GetMemberValueConvertible), BindingFlags.Static | BindingFlags.NonPublic)!;
            private static readonly MethodInfo _getValuesMethod = typeof(RoutingMember).GetMethod(nameof(GetMemberValues), BindingFlags.Static | BindingFlags.NonPublic)!;
            private static readonly MethodInfo _getValuesConvertibleMethod = typeof(RoutingMember).GetMethod(nameof(GetMemberValuesConvertible), BindingFlags.Static | BindingFlags.NonPublic)!;
            private static readonly MethodInfo _matchesMethod = typeof(RoutingContentValue).GetMethod(nameof(RoutingContentValue.Matches))!;

            public static ParameterExpression ParameterExpression { get; } = Expression.Parameter(typeof(IMessage), "m");

            public static RoutingMember[] GetAll(Type messageType)
            {
                return messageType.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                                  .Select(x => (member: x, attribute: x.GetCustomAttribute<RoutingPositionAttribute>(true)))
                                  .Where(x => x.attribute != null)
                                  .OrderBy(x => x.attribute!.Position)
                                  .Select((x, index) => new RoutingMember(index, x.attribute!.Position, x.member))
                                  .ToArray();
            }

            private readonly MethodCallExpression _valueExpression;
            private readonly Func<IMessage, RoutingContentValue> _valueFunc;

            private RoutingMember(int index, int position, MemberInfo member)
            {
                Index = index;
                RoutingPosition = position;
                Member = member;

                var valueExpression = BuildValueExpression(member);

                _valueExpression = valueExpression;
                _valueFunc = BuildValueFunc(valueExpression);
            }

            public int Index { get; }
            public int RoutingPosition { get; }
            public MemberInfo Member { get; }

            public RoutingContentValue GetValue(IMessage message)
            {
                try
                {
                    return _valueFunc(message);
                }
                catch (NullReferenceException)
                {
                    throw new InvalidOperationException($"Message of type {message.GetType().Name} is not valid. Member {Member.Name} part of the routing key at position {RoutingPosition} can not be null");
                }
            }

            private static RoutingContentValue GetMemberValue<TMember>(TMember? value)
                => new RoutingContentValue(value?.ToString());

            private static RoutingContentValue GetMemberValueConvertible<TMember>(TMember? value)
                where TMember : IConvertible
                => new RoutingContentValue(value?.ToString(CultureInfo.InvariantCulture));

            private static RoutingContentValue GetMemberValues<TMember>(ICollection<TMember>? values)
                => new RoutingContentValue(values != null ? values.Select(x => x!.ToString()).ToArray() : Array.Empty<string>());

            private static RoutingContentValue GetMemberValuesConvertible<TMember>(ICollection<TMember>? values)
                where TMember : IConvertible
                => new RoutingContentValue(values != null ? values.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToArray() : Array.Empty<string>());

            private static MethodCallExpression BuildValueExpression(MemberInfo member)
            {
                var memberExpression = GetMemberExpression();
                var getValueMethod = GetValueMethodInfo(memberExpression.type);

                return Expression.Call(null, getValueMethod, memberExpression.value);

                (Expression value, Type type) GetMemberExpression()
                {
                    var castExpression = Expression.Convert(ParameterExpression, member.DeclaringType!);

                    return member switch
                    {
                        PropertyInfo propertyInfo => (Expression.Property(castExpression, propertyInfo), propertyInfo.PropertyType),
                        FieldInfo fieldInfo       => (Expression.Field(castExpression, fieldInfo), fieldInfo.FieldType),
                        _                         => throw new InvalidOperationException("Cannot define routing position on a member other than a field or property"),
                    };
                }

                MethodInfo GetValueMethodInfo(Type memberType)
                {
                    var collectionType = memberType.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(ICollection<>));

                    if (collectionType != null)
                    {
                        var itemType = collectionType.GetGenericArguments()[0];
                        return typeof(IConvertible).IsAssignableFrom(itemType) ? _getValuesConvertibleMethod.MakeGenericMethod(itemType) : _getValuesMethod.MakeGenericMethod(itemType);
                    }

                    return typeof(IConvertible).IsAssignableFrom(memberType) ? _getValueConvertibleMethod.MakeGenericMethod(memberType) : _getValueMethod.MakeGenericMethod(memberType);
                }
            }

            private static Func<IMessage, RoutingContentValue> BuildValueFunc(Expression valueExpression)
            {
                var lambda = Expression.Lambda(typeof(Func<IMessage, RoutingContentValue>), valueExpression, ParameterExpression);
                return (Func<IMessage, RoutingContentValue>)lambda.Compile();
            }

            public Expression CreateMatchExpression(string? targetValue)
            {
                return Expression.Call(_valueExpression, _matchesMethod, Expression.Constant(targetValue));
            }
        }
    }
}
