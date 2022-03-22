﻿using System;
using Abc.Zebus.Serialization.Protobuf;
using Abc.Zebus.Transport;

namespace Abc.Zebus.Persistence.Storage
{
    /// <summary>
    /// Stateful, non thread-safe serializer.
    /// </summary>
    public class TransportMessageSerializer
    {
        private readonly int _maximumCapacity;
        private ProtoBufferWriter _bufferWriter;

        public TransportMessageSerializer(int maximumCapacity = 50 * 1024)
        {
            _maximumCapacity = maximumCapacity;
            _bufferWriter = new ProtoBufferWriter();
        }

        public byte[] Serialize(TransportMessage transportMessage)
        {
            _bufferWriter.Reset();
            _bufferWriter.WriteTransportMessage(transportMessage);

            var bytes = _bufferWriter.Buffer.AsSpan(0, _bufferWriter.Position).ToArray();

            // prevent service from leaking after fat transport message serializations
            if (_bufferWriter.Position > _maximumCapacity)
                _bufferWriter = new ProtoBufferWriter(new byte[_maximumCapacity]);

            return bytes;
        }


    }
}
