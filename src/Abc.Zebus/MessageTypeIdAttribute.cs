using System;

namespace Abc.Zebus
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class MessageTypeIdAttribute : Attribute
    {
        public Guid MessageTypeId { get; private set; }

        public MessageTypeIdAttribute(string typeId)
        {
            MessageTypeId = Guid.Parse(typeId);
        }
    }
}
