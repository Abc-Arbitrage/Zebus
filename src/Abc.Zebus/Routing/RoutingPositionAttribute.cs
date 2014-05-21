using System;

namespace Abc.Zebus.Routing
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class RoutingPositionAttribute : Attribute
    {
        public int Position { get; private set; }

        public RoutingPositionAttribute(int position)
        {
            Position = position;
        }
    }
}