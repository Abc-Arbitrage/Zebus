using System;
using Abc.Zebus.Serialization.Protobuf;
using Abc.Zebus.Transport;

namespace Abc.Zebus.Persistence.Storage
{
    /// <summary>
    /// Stateful, non thread-safe serializer.
    /// </summary>
    internal class TransportMessageSerializer
    {
        private readonly int _maximumCapacity;
        private ProtoBufferWriter _outputStream;

        public TransportMessageSerializer(int maximumCapacity = 50 * 1024)
        {
            _maximumCapacity = maximumCapacity;
            _outputStream = new ProtoBufferWriter();
        }

        public byte[] Serialize(TransportMessage transportMessage)
        {
            _outputStream.Reset();
            _outputStream.WriteTransportMessage(transportMessage);

            var bytes = new byte[_outputStream.Position];
            Buffer.BlockCopy(_outputStream.Buffer, 0, bytes, 0, _outputStream.Position);

            // prevent service from leaking after fat transport message serializations
            if (_outputStream.Position > _maximumCapacity)
                _outputStream = new ProtoBufferWriter(new byte[_maximumCapacity]);

            return bytes;
        }
    }
}
