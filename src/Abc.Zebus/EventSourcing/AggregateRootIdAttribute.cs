using System;

namespace Abc.Zebus.EventSourcing
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class AggregateRootIdAttribute : Attribute
    {
    }
}