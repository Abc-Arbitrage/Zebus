using System;

namespace Abc.Zebus.EventSourcing
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class AggregateRootIdAttribute : Attribute
    {
    }
}
