using System;

namespace Abc.Zebus
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class MessageTypeIdAttribute : Attribute
    {
        public Guid MessageTypeId { get; }

        public MessageTypeIdAttribute(string typeId)
        {
            MessageTypeId = Guid.Parse(typeId);
        }
    }
}
