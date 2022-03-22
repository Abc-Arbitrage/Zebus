using System;
using Abc.Zebus.Serialization.Protobuf;
using Abc.Zebus.Transport;

namespace Abc.Zebus.Persistence.Storage
{
    [Obsolete("Use TransportMessage instead")]
    public static class TransportMessageConvert
    {
        public static byte[] Serialize(TransportMessage transportMessage)
        {
            var writer = new ProtoBufferWriter();
            writer.WriteTransportMessage(transportMessage);

            return writer.Buffer.AsSpan(0, writer.Position).ToArray();
        }

        public static TransportMessage Deserialize(byte[] bytes)
        {
            var reader = new ProtoBufferReader(bytes, bytes.Length);
            return reader.ReadTransportMessage();
        }
    }
}
