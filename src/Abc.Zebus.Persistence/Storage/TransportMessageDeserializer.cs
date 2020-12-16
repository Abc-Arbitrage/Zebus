using Abc.Zebus.Serialization.Protobuf;
using Abc.Zebus.Transport;

namespace Abc.Zebus.Persistence.Storage
{
    internal static class TransportMessageDeserializer
    {
        public static TransportMessage Deserialize(byte[] bytes)
        {
            var bufferReader = new ProtoBufferReader(bytes, bytes.Length);
            return bufferReader.ReadTransportMessage();
        }
    }
}
