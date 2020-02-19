using System;
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

        public bool TryGetRoutingMemberByName(string memberName, [NotNullWhen(true)] out RoutingMember? routingMember)
        {
            foreach (var member in RoutingMembers)
            {
                if (member.Member.Name == memberName)
                {
                    routingMember = member;
                    return true;
                }
            }

            routingMember = default;
            return false;
        }

        public static MessageTypeDescriptor Load(string? fullName)
        {
            if (fullName == null)
                return Null;

            var messageType = TypeUtil.Resolve(fullName);
            if (messageType == null)
                return new MessageTypeDescriptor(fullName);

            var isPersistent = !Attribute.IsDefined(messageType, typeof(TransientAttribute));
            var isInfrastructure = Attribute.IsDefined(messageType, typeof(InfrastructureAttribute));
            var routingMembers =  RoutingMember.GetAll(messageType);

            return new MessageTypeDescriptor(fullName, messageType, isPersistent, isInfrastructure, routingMembers);
        }

        public class RoutingMember
        {
            private static readonly MethodInfo _toStringMethod = typeof(object).GetMethod("ToString")!;
            private static readonly MethodInfo _toStringWithFormatMethod = typeof(IConvertible).GetMethod("ToString")!;

            public static ParameterExpression ParameterExpression { get; } = Expression.Parameter(typeof(IMessage), "m");

            public static RoutingMember[] GetAll(Type messageType)
            {
                var castExpression = Expression.Convert(ParameterExpression, messageType);

                return messageType.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                                  .Select(x => (member: x, attribute: x.GetCustomAttribute<RoutingPositionAttribute>(true)))
                                  .Where(x => x.attribute != null)
                                  .OrderBy(x => x.attribute!.Position)
                                  .Select((x, index) => new RoutingMember(index, x.attribute!.Position, x.member, BuildToStringExpression(castExpression, x.member)))
                                  .ToArray();
            }

            private RoutingMember(int index, int position, MemberInfo member, MethodCallExpression toStringExpression)
            {
                Index = index;
                RoutingPosition = position;
                Member = member;
                ToStringExpression = toStringExpression;
                ToStringDelegate = BuildToStringDelegate(toStringExpression);
            }

            public int Index { get; }
            public int RoutingPosition { get; }
            public MemberInfo Member { get; }
            public MethodCallExpression ToStringExpression { get; }
            public Func<IMessage, string> ToStringDelegate { get; }

            public string GetValue(IMessage message)
            {
                try
                {
                    return ToStringDelegate(message);
                }
                catch (NullReferenceException)
                {
                    throw new InvalidOperationException($"Message of type {message.GetType().Name} is not valid. Member {Member.Name} part of the routing key at position {RoutingPosition} can not be null");
                }
            }

            private static MethodCallExpression BuildToStringExpression(Expression castExpression, MemberInfo member)
            {
                Func<Expression, Expression> memberAccessor;
                Type memberType;
                if (member.MemberType == MemberTypes.Property)
                {
                    var propertyInfo = (PropertyInfo)member;
                    memberAccessor = m => Expression.Property(m, propertyInfo);
                    memberType = propertyInfo.PropertyType;
                }
                else if (member.MemberType == MemberTypes.Field)
                {
                    var fieldInfo = (FieldInfo)member;
                    memberAccessor = m => Expression.Field(m, fieldInfo);
                    memberType = fieldInfo.FieldType;
                }
                else
                {
                    throw new InvalidOperationException("Cannot define routing position on a member other than a field or property");
                }

                return typeof(IConvertible).IsAssignableFrom(memberType) && memberType != typeof(string)
                    ? Expression.Call(memberAccessor(castExpression), _toStringWithFormatMethod, Expression.Constant(CultureInfo.InvariantCulture))
                    : Expression.Call(memberAccessor(castExpression), _toStringMethod);
            }

            private static Func<IMessage, string> BuildToStringDelegate(Expression toStringExpression)
            {
                var lambda = Expression.Lambda(typeof(Func<IMessage, string>), toStringExpression, ParameterExpression);
                return (Func<IMessage, string>)lambda.Compile();
            }
        }
    }
}
