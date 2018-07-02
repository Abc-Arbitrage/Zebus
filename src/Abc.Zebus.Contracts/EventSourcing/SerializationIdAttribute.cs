using System;

namespace Abc.Zebus.EventSourcing
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class SerializationIdAttribute : Attribute
    {
        public string FullName { get; }

        public SerializationIdAttribute(string fullName)
        {
            FullName = fullName;
        }
    }
}
