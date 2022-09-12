using System;
using System.IO;
using System.Text;
using Abc.Zebus.Util;

namespace Abc.Zebus.Serialization.Protobuf
{
    internal sealed class ProtoBufferWriter
    {
        internal static readonly Encoding Utf8Encoding = Encoding.UTF8;

        public const int GuidSize = 18;

        private byte[] _buffer;
        private int _position;
        private int? _savedPosition;

        public ProtoBufferWriter() : this(new byte[4096])
        {
        }

        public ProtoBufferWriter(byte[] buffer)
        {
            _buffer = buffer;
        }

        public byte[] Buffer => _buffer;
        public int Position => _position;

        public void Reset()
        {
            _position = 0;
            _savedPosition = null;
        }

        public byte[] ToArray()
        {
            return Buffer.AsSpan(0, _position).ToArray();
        }

        public void SavePosition()
        {
            _savedPosition = _position;
        }

        public bool TryWriteBoolAtSavedPosition(bool value)
        {
            if (_savedPosition == null)
                return false;

            _buffer[_savedPosition.Value] = value ? (byte)1 : (byte)0;
            return true;
        }

        public void WriteBool(bool value)
        {
            WriteRawByte(value ? (byte) 1 : (byte) 0);
        }

        public void WriteString(string value, int length)
        {
            WriteLength(length);
            if (_buffer.Length - _position >= length)
            {
                if (length == value.Length)
                {
                    for (int i = 0; i < length; i++)
                    {
                        _buffer[_position + i] = (byte)value[i];
                    }
                }
                else
                {
                    Utf8Encoding.GetBytes(value, 0, value.Length, _buffer, _position);
                }

                _position += length;
            }
            else
            {
                byte[] bytes = Utf8Encoding.GetBytes(value);
                WriteRawBytes(bytes.AsSpan());
            }
        }

        public void WriteLength(int length)
        {
            WriteRawVarint32((uint) length);
        }

        public void WriteRawTag(byte b1)
        {
            WriteRawByte(b1);
        }

        internal void WriteRawVarint32(uint value)
        {
            if (value < 128 && _position < _buffer.Length)
            {
                _buffer[_position++] = (byte)value;
                return;
            }

            while (value > 127 && _position < _buffer.Length)
            {
                _buffer[_position++] = (byte) ((value & 0x7F) | 0x80);
                value >>= 7;
            }
            while (value > 127)
            {
                WriteRawByte((byte) ((value & 0x7F) | 0x80));
                value >>= 7;
            }
            if (_position < _buffer.Length)
            {
                _buffer[_position++] = (byte) value;
            }
            else
            {
                WriteRawByte((byte) value);
            }
        }

        internal void WriteRawByte(byte value)
        {
            EnsureCapacity(1);

            _buffer[_position++] = value;
        }

        public void WriteGuid(Guid value)
        {
            EnsureCapacity(GuidSize + 1);

            _buffer[_position++] = GuidSize;

            var blob = value.ToByteArray();
            _buffer[_position++] = 1 << 3 | 1;
            for (var i = 0; i < 8; i++)
            {
                _buffer[_position++] = blob[i];
            }
            _buffer[_position++] = 2 << 3 | 1;
            for (var i = 8; i < 16; i++)
            {
                _buffer[_position++] = blob[i];
            }
        }

        public void WriteRawBytes(ReadOnlyMemory<byte> bytes)
        {
            WriteRawBytes(bytes.Span);
        }

        public void WriteRawBytes(ReadOnlySpan<byte> bytes)
        {
            var length = bytes.Length;
            EnsureCapacity(length);

            bytes.CopyTo(_buffer.AsSpan(_position));
            _position += length;
        }

        private void EnsureCapacity(int length)
        {
            if (_buffer.Length - _position >= length)
                return;

            var newBufferLength = Math.Max(_position + length, _buffer.Length * 2);
            var newBuffer = new byte[newBufferLength];
            _buffer.AsSpan().CopyTo(newBuffer);

            _buffer = newBuffer;
        }

        public static int ComputeStringSize(int byteArraySize)
        {
            return ComputeLengthSize(byteArraySize) + byteArraySize;
        }

        public static int ComputeLengthSize(int length)
        {
            return ComputeRawVarint32Size((uint) length);
        }

        public static int ComputeRawVarint32Size(uint value)
        {
            if ((value & (0xffffffff << 7)) == 0)
            {
                return 1;
            }
            if ((value & (0xffffffff << 14)) == 0)
            {
                return 2;
            }
            if ((value & (0xffffffff << 21)) == 0)
            {
                return 3;
            }
            if ((value & (0xffffffff << 28)) == 0)
            {
                return 4;
            }
            return 5;
        }
    }
}
