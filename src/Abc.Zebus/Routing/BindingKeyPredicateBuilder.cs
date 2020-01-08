using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Abc.Zebus.Routing
{
    public class BindingKeyPredicateBuilder : IBindingKeyPredicateBuilder
    {
        private static readonly MethodInfo _toStringMethod = typeof(object).GetMethod("ToString");
        private static readonly MethodInfo _toStringWithFormatMethod = typeof(IConvertible).GetMethod("ToString");
        private readonly ConcurrentDictionary<Type, CacheItem> _cacheItems = new ConcurrentDictionary<Type, CacheItem>();

        class CacheItem
        {
            public ParameterExpression ParameterExpression { get; set; }
            public IList<MethodCallExpression> MembersToStringExpressions { get; set; }
        }

        public Func<IMessage, bool> GetPredicate(Type messageType, BindingKey bindingKey)
        {
            if (bindingKey.IsEmpty)
                return _ => true;

            var cacheItem = GetOrCreateCacheItem(messageType);

            var count = Math.Min(cacheItem.MembersToStringExpressions.Count, bindingKey.PartCount);
            var subPredicates = new List<Expression>();
            for (var index = 0; index < count; index++)
            {
                if (bindingKey.IsSharp(index))
                    break;

                if (bindingKey.IsStar(index))
                    continue;

                var part = bindingKey.GetPart(index);
                var memberToStringExpression = cacheItem.MembersToStringExpressions[index];
                subPredicates.Add(Expression.MakeBinary(ExpressionType.Equal, memberToStringExpression, Expression.Constant(part)));
            }

            if (!subPredicates.Any())
                return _ => true;

            var finalExpression = subPredicates.Aggregate((Expression)null, (final, exp) => final == null ? exp : Expression.AndAlso(final, exp));
            return (Func<IMessage, bool>)Expression.Lambda(finalExpression, cacheItem.ParameterExpression).Compile();
        }

        private CacheItem GetOrCreateCacheItem(Type messageType)
        {
            return _cacheItems.GetOrAdd(messageType, type =>
            {
                var routingMembers = GetRoutingMembers(type);
                var parameterExpression = Expression.Parameter(typeof(IMessage), "m");
                var castedMessage = Expression.Convert(parameterExpression, messageType);

                return new CacheItem
                {
                    ParameterExpression = parameterExpression,
                    MembersToStringExpressions = routingMembers.Select(x => GenerateMemberToStringExpression(castedMessage, x)).ToList()
                };
            });
        }

        public static List<MemberExtendedInfo> GetRoutingMembers(Type type)
        {
            return type.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                       .Select(x => new MemberExtendedInfo { Member = x, Attribute = x.GetCustomAttribute<RoutingPositionAttribute>(true) })
                       .Where(x => x.Attribute != null)
                       .OrderBy(x => x.Attribute.Position)
                       .ToList();
        }

        private static MethodCallExpression GenerateMemberToStringExpression(Expression parameterExpression, MemberExtendedInfo memberExtendedInfo)
        {
            Func<Expression, Expression> memberAccessor;
            Type memberType;
            var memberInfo = memberExtendedInfo.Member;
            if (memberInfo.MemberType == MemberTypes.Property)
            {
                var propertyInfo = (PropertyInfo)memberInfo;
                memberAccessor = m => Expression.Property(m, propertyInfo);
                memberType = propertyInfo.PropertyType;
            }
            else if (memberInfo.MemberType == MemberTypes.Field)
            {
                var fieldInfo = (FieldInfo)memberInfo;
                memberAccessor = m => Expression.Field(m, fieldInfo);
                memberType = fieldInfo.FieldType;
            }
            else
                throw new InvalidOperationException("Cannot define routing position on a member other than a field or property");

            var getMemberValue = typeof(IConvertible).IsAssignableFrom(memberType) && memberType != typeof(string)
                ? Expression.Call(memberAccessor(parameterExpression), _toStringWithFormatMethod, Expression.Constant(CultureInfo.InvariantCulture))
                : Expression.Call(memberAccessor(parameterExpression), _toStringMethod);
            return getMemberValue;
        }
    }

    public class MemberExtendedInfo
    {
        public MemberInfo Member { get; set; }
        public RoutingPositionAttribute Attribute { get; set; }
    }
}
