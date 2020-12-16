using Abc.Zebus.Serialization.Protobuf;
using Abc.Zebus.Transport;

namespace Abc.Zebus.Persistence.Storage
{
    internal static class TransportMessageDeserializer
    {
        public static TransportMessage Deserialize(byte[] bytes)
        {
            var inputStream = new ProtoBufferReader(bytes, 0, bytes.Length);
            return inputStream.ReadTransportMessage();
        }
    }
}