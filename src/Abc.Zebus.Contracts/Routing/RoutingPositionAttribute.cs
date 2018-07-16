using System;

namespace Abc.Zebus.Routing
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class RoutingPositionAttribute : Attribute
    {
        public int Position { get; }

        public RoutingPositionAttribute(int position)
        {
            Position = position;
        }
    }
}
