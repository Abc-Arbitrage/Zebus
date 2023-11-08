using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Abc.Zebus.Routing;

public static class BindingKeyUtil
{
    public static Func<IMessage, bool> BuildPredicate(MessageTypeId messageTypeId, BindingKey bindingKey)
    {
        if (bindingKey.IsEmpty)
            return _ => true;

        var routingMembers = messageTypeId.Descriptor.RoutingMembers;
        var count = Math.Min(routingMembers.Length, bindingKey.PartCount);
        var subPredicates = new List<Expression>();
        for (var index = 0; index < count; index++)
        {
            if (bindingKey.IsSharp(index))
                break;

            if (bindingKey.IsStar(index))
                continue;

            var part = bindingKey.GetPartToken(index);

            subPredicates.Add(routingMembers[index].CreateMatchExpression(part));
        }

        if (!subPredicates.Any())
            return _ => true;

        var empty = Expression.Empty();
        var finalExpression = subPredicates.Aggregate<Expression, Expression>(empty, (final, exp) => final == empty ? exp : Expression.AndAlso(final, exp));

        return (Func<IMessage, bool>)Expression.Lambda(finalExpression, MessageTypeDescriptor.RoutingMember.ParameterExpression).Compile();
    }

    public static BindingKeyPart GetPartForMember(MessageTypeId messageTypeId, string memberName, BindingKey bindingKey)
    {
        var routingMember = GetRoutingMemberOrThrow(messageTypeId, memberName);

        return bindingKey.GetPart(routingMember.Index);
    }

    public static IEnumerable<BindingKeyPart> GetPartsForMember(MessageTypeId messageTypeId, string memberName, IEnumerable<BindingKey> bindingKeys)
    {
        var routingMember = GetRoutingMemberOrThrow(messageTypeId, memberName);

        return bindingKeys.Select(x => x.GetPart(routingMember.Index));
    }

    private static MessageTypeDescriptor.RoutingMember GetRoutingMemberOrThrow(MessageTypeId messageTypeId, string memberName)
    {
        if (!messageTypeId.Descriptor.TryGetRoutingMemberByName(memberName, out var routingMember))
            throw new InvalidOperationException($"Unable to find routing member named {memberName} in type {messageTypeId.FullName}");

        return routingMember;
    }
}
