using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Routing;

namespace Abc.Zebus.Directory
{
    public class SubscriptionDefinition
    {
        public IReadOnlyList<SubscriptionDefinitionPart> Parts { get; }

        public bool MatchesAll => Parts.Count == 0;

        public SubscriptionDefinition(Type type, BindingKey bindingKey)
        {
            var routingMembers = BindingKeyPredicateBuilder.GetRoutingMembers(type);
            Parts = bindingKey.GetParts()
                              .Zip(routingMembers, (p, m) => (p, m))
                              .Select((x, i) => new SubscriptionDefinitionPart(x.m.Member.Name, x.p, bindingKey.IsSharp(i) || bindingKey.IsStar(i)))
                              .ToList();
        }

        public SubscriptionDefinitionMatch? GetMatchForRoutingKeyPart(string propertyName)
        {
            var part = Parts.SingleOrDefault(x => x.PropertyName == propertyName);
            return part.PropertyName == propertyName ? part.Match : (SubscriptionDefinitionMatch?)null;
        }
    }
}
