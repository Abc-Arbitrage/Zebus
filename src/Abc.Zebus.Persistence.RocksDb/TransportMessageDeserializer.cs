using Abc.Zebus.Serialization.Protobuf;
using Abc.Zebus.Transport;

namespace Abc.Zebus.Persistence.RocksDb
{
    internal static class TransportMessageDeserializer
    {
        public static TransportMessage Deserialize(byte[] bytes)
        {
            var inputStream = new CodedInputStream(bytes, 0, bytes.Length);
            var readTransportMessage = inputStream.ReadTransportMessage();
            return readTransportMessage;
        }
    }
}