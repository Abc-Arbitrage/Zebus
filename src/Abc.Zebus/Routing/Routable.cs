using System;

namespace Abc.Zebus.Routing
{
    public class Routable : Attribute
    {
        public RoutingType RoutingType { get; set; }
    }

    public enum RoutingType
    {
        Topic,
        Direct,
        Fanout,
    }
}