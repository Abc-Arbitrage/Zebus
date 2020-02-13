using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Abc.Zebus.Routing
{
    public static class BindingKeyPredicateBuilder
    {
        public static Func<IMessage, bool> BuildPredicate(Type messageType, BindingKey bindingKey)
        {
            return BuildPredicate(MessageUtil.GetTypeId(messageType), bindingKey);
        }

        public static Func<IMessage, bool> BuildPredicate(MessageTypeId messageTypeId, BindingKey bindingKey)
        {
            if (bindingKey.IsEmpty)
                return _ => true;

            var routingMembers = messageTypeId.Descriptor.RoutingMembers;
            var count = Math.Min(routingMembers.Count, bindingKey.PartCount);
            var subPredicates = new List<Expression>();
            for (var index = 0; index < count; index++)
            {
                if (bindingKey.IsSharp(index))
                    break;

                if (bindingKey.IsStar(index))
                    continue;

                var part = bindingKey.GetPart(index);
                var memberToStringExpression = routingMembers[index].ToStringExpression;
                subPredicates.Add(Expression.MakeBinary(ExpressionType.Equal, memberToStringExpression, Expression.Constant(part)));
            }

            if (!subPredicates.Any())
                return _ => true;

            var finalExpression = subPredicates.Aggregate((Expression)null, (final, exp) => final == null ? exp : Expression.AndAlso(final, exp));
            return (Func<IMessage, bool>)Expression.Lambda(finalExpression, MessageTypeDescriptor.RoutingMember.ParameterExpression).Compile();
        }
    }
}
