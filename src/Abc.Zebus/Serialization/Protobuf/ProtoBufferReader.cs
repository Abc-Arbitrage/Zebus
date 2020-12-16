using System;
using System.Runtime.CompilerServices;
using Abc.Zebus.Util;
using ProtoBuf;

namespace Abc.Zebus.Serialization.Protobuf
{
    internal sealed class ProtoBufferReader
    {
        private readonly byte[] _guidBuffer = new byte[16];
        private readonly byte[] _buffer;
        private readonly int _size;
        private int _position;

        public ProtoBufferReader(byte[] buffer, int length)
        {
            _buffer = buffer;
            _size = length;
        }

        public int Position => _position;

        public int Length => _size;

        public void Reset() => _position = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanRead(int length) => _size - _position >= length;

        public bool TryReadTag(out uint number, out WireType wireType)
        {
            if (!TryReadTag(out var tag))
            {
                number = 0;
                wireType = WireType.None;
                return false;
            }
            number = tag >> 3;
            wireType = (WireType)(tag & 7);
            return true;
        }

        public bool TryReadTag(out uint value)
        {
            if (!CanRead(1))
            {
                value = default;
                return false;
            }

            var tag = _buffer[_position];
            if (tag < 128)
            {
                _position++;
                value = tag;
                return value != 0;
            }

            return TryReadRawVariant(out value);
        }

        public bool TryReadFixed64(out ulong value)
        {
            return TryReadRawLittleEndian64(out value);
        }

        public bool TryReadFixed32(out uint value)
        {
            return TryReadRawLittleEndian32(out value);
        }

        public bool TryReadBool(out bool value)
        {
            var success = TryReadRawVariant(out var variant);
            value = variant != 0;
            return success;
        }

        public bool TryReadString(out string? s)
        {
            if (!TryReadLength(out var length) || !CanRead(length))
            {
                s = default;
                return false;
            }

            if (length == 0)
            {
                s = "";
                return true;
            }

            var result = ProtoBufferWriter.Utf8Encoding.GetString(_buffer, _position, length);
            _position += length;

            s = result;
            return true;
        }

        public bool TrySkipString()
        {
            if (!TryReadLength(out var length) || !CanRead(length))
                return false;

            _position += length;
            return true;
        }

        public bool TryReadGuid(out Guid value)
        {
            if (!TryReadLength(out var length) || !CanRead(length) || length != ProtoBufferWriter.GuidSize)
                return false;

            // Skip tag
            ByteUtil.Copy(_buffer, _position + 1, _guidBuffer, 0, 8);
            // Skip tag
            ByteUtil.Copy(_buffer, _position + 10, _guidBuffer, 8, 8);

            _position += ProtoBufferWriter.GuidSize;

            value = new Guid(_guidBuffer);
            return true;
        }

        public bool TryReadLength(out int length)
        {
            var success = TryReadRawVariant(out var variant);
            length = (int)variant;

            return success;
        }

        internal bool TryReadRawVariant(out uint value)
        {
            var available = _size - _position;
            if (available <= 0)
            {
                value = default;
                return false;
            }

            value = _buffer[_position++];
            if ((value & 0x80) == 0)
                return true;

            value &= 0x7F;

            if (available == 1)
                return false;

            uint chunk = _buffer[_position++];
            value |= (chunk & 0x7F) << 7;
            if ((chunk & 0x80) == 0)
                return true;

            if (available == 2)
                return false;

            chunk = _buffer[_position++];
            value |= (chunk & 0x7F) << 14;
            if ((chunk & 0x80) == 0)
                return true;

            if (available == 3)
                return false;

            chunk = _buffer[_position++];
            value |= (chunk & 0x7F) << 21;
            if ((chunk & 0x80) == 0)
                return true;

            return false;
        }

        private bool TryReadRawLittleEndian32(out uint value)
        {
            if (!CanRead(4))
            {
                value = default;
                return false;
            }

            uint b1 = _buffer[_position++];
            uint b2 = _buffer[_position++];
            uint b3 = _buffer[_position++];
            uint b4 = _buffer[_position++];
            value = b1 | (b2 << 8) | (b3 << 16) | (b4 << 24);
            return true;
        }

        /// <summary>
        /// Reads a 64-bit little-endian integer from the stream.
        /// </summary>
        private bool TryReadRawLittleEndian64(out ulong value)
        {
            if (!CanRead(8))
            {
                value = default;
                return false;
            }

            ulong b1 = _buffer[_position++];
            ulong b2 = _buffer[_position++];
            ulong b3 = _buffer[_position++];
            ulong b4 = _buffer[_position++];
            ulong b5 = _buffer[_position++];
            ulong b6 = _buffer[_position++];
            ulong b7 = _buffer[_position++];
            ulong b8 = _buffer[_position++];
            value =  b1 | (b2 << 8) | (b3 << 16) | (b4 << 24) | (b5 << 32) | (b6 << 40) | (b7 << 48) | (b8 << 56);
            return true;
        }

        internal bool TryReadRawBytes(int size, out byte[] value)
        {
            if (size < 0 || !CanRead(size))
            {
                value = Array.Empty<byte>();
                return false;
            }

            value = new byte[size];
            ByteUtil.Copy(_buffer, _position, value, 0, size);
            _position += size;
            return true;
        }
    }
}
