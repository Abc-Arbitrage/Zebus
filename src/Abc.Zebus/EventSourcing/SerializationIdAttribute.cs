using System;

namespace Abc.Zebus.EventSourcing
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class SerializationIdAttribute : Attribute
    {
        public string FullName { get; private set; }

        public SerializationIdAttribute(string fullName)
        {
            FullName = fullName;
        }
    }
}