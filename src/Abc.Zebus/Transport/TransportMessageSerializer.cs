using System;
using Abc.Zebus.Serialization.Protobuf;

namespace Abc.Zebus.Transport;

/// <summary>
/// Stateful, non thread-safe serializer.
/// </summary>
public class TransportMessageSerializer
{
    private readonly byte[] _boundedBuffer;
    private ProtoBufferWriter _bufferWriter;

    public TransportMessageSerializer()
    {
        _boundedBuffer = Array.Empty<byte>();
        _bufferWriter = new ProtoBufferWriter();
    }

    public TransportMessageSerializer(int maximumCapacity)
    {
        _boundedBuffer = new byte[maximumCapacity];
        _bufferWriter = new ProtoBufferWriter(_boundedBuffer);
    }

    public byte[] Serialize(TransportMessage transportMessage)
    {
        _bufferWriter.Reset();
        _bufferWriter.WriteTransportMessage(transportMessage);

        var bytes = _bufferWriter.ToArray();

        // Prevent service from leaking after large transport message serializations.

        if (_boundedBuffer.Length != 0 && _bufferWriter.Buffer != _boundedBuffer)
            _bufferWriter = new ProtoBufferWriter(_boundedBuffer);

        return bytes;
    }
}
