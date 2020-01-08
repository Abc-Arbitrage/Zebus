using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Routing;

namespace Abc.Zebus.Directory
{
    public class SubscriptionDefinition
    {
        public List<SubscriptionDefinitionPart> Parts { get; }

        public SubscriptionDefinition(Type type, BindingKey bindingKey)
        {
            var routingMembers = BindingKeyPredicateBuilder.GetRoutingMembers(type);
            Parts = bindingKey.GetParts()
                              .Zip(routingMembers, (p, m) => (p, m))
                              .Select((x, i) => new SubscriptionDefinitionPart(x.m.Member.Name, x.p, bindingKey.IsSharp(i) || bindingKey.IsStar(i)))
                              .ToList();
        }
    }
}