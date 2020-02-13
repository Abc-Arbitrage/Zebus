using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Abc.Zebus.Routing;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Extensions;

namespace Abc.Zebus
{
    internal class MessageTypeDescriptor
    {
        public static readonly MessageTypeDescriptor Null = new MessageTypeDescriptor(null);

        private MessageTypeDescriptor(string fullName, Type messageType = null, bool isPersistent = true, bool isInfrastructure = false, IEnumerable<RoutingMember> routingMembers = null)
        {
            FullName = fullName;
            MessageType = messageType;
            IsPersistent = isPersistent;
            IsInfrastructure = isInfrastructure;
            RoutingMembers = routingMembers.EmptyIfNull().OrderBy(x => x.RoutingPosition).ToList();
        }

        public string FullName { get; }
        public Type MessageType { get; }
        public bool IsPersistent { get; }
        public bool IsInfrastructure { get; }
        public IReadOnlyList<RoutingMember> RoutingMembers { get; }

        public static MessageTypeDescriptor Load(string fullName)
        {
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
            private static readonly MethodInfo _toStringMethod = typeof(object).GetMethod("ToString");
            private static readonly MethodInfo _toStringWithFormatMethod = typeof(IConvertible).GetMethod("ToString");

            public static ParameterExpression ParameterExpression { get; } = Expression.Parameter(typeof(IMessage), "m");

            public static IEnumerable<RoutingMember> GetAll(Type messageType)
            {
                var castExpression = Expression.Convert(ParameterExpression, messageType);

                return messageType.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                                  .Where(x => Attribute.IsDefined(x, typeof(RoutingPositionAttribute), true))
                                  .Select(x => new RoutingMember(x.GetCustomAttribute<RoutingPositionAttribute>(true).Position, x, BuildToStringExpression(castExpression, x)));
            }

            private RoutingMember(int routingPosition, MemberInfo member, MethodCallExpression toStringExpression)
            {
                RoutingPosition = routingPosition;
                Member = member;
                ToStringExpression = toStringExpression;
            }

            public int RoutingPosition { get; }
            public MemberInfo Member { get; }
            public MethodCallExpression ToStringExpression { get; }

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

                var getMemberValue = typeof(IConvertible).IsAssignableFrom(memberType) && memberType != typeof(string)
                    ? Expression.Call(memberAccessor(castExpression), _toStringWithFormatMethod, Expression.Constant(CultureInfo.InvariantCulture))
                    : Expression.Call(memberAccessor(castExpression), _toStringMethod);

                return getMemberValue;
            }
        }
    }
}
