using System;

namespace Abc.Zebus.Serialization
{
    public class ProtocolBufferSerializationException : Exception
    {
        public object MessageToSerialize { get; private set; }

        public ProtocolBufferSerializationException(object message, Exception exception) 
            : base("Unable to serialize message. See inner exception for more details", exception)
        {
            MessageToSerialize = message;
        }
    }
}