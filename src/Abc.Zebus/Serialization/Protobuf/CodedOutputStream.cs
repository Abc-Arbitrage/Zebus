#region Copyright notice and license
// Protocol Buffers - Google's data interchange format
// Copyright 2008 Google Inc.  All rights reserved.
// https://developers.google.com/protocol-buffers/
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
//
//     * Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above
// copyright notice, this list of conditions and the following disclaimer
// in the documentation and/or other materials provided with the
// distribution.
//     * Neither the name of Google Inc. nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

using System;
using System.IO;
using System.Text;

namespace Abc.Zebus.Serialization.Protobuf
{
    /// <summary>
    /// Encodes and writes protocol message fields.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is generally used by generated code to write appropriate
    /// primitives to the stream. It effectively encapsulates the lowest
    /// levels of protocol buffer format. Unlike some other implementations,
    /// this does not include combined "write tag and value" methods. Generated
    /// code knows the exact byte representations of the tags they're going to write,
    /// so there's no need to re-encode them each time. Manually-written code calling
    /// this class should just call one of the <c>WriteTag</c> overloads before each value.
    /// </para>
    /// <para>
    /// Repeated fields and map fields are not handled by this class; use <c>RepeatedField&lt;T&gt;</c>
    /// and <c>MapField&lt;TKey, TValue&gt;</c> to serialize such fields.
    /// </para>
    /// </remarks>
    internal sealed partial class CodedOutputStream
    {
        // "Local" copy of Encoding.UTF8, for efficiency. (Yes, it makes a difference.)
        internal static readonly Encoding Utf8Encoding = Encoding.UTF8;

        /// <summary>
        /// The buffer size used by CreateInstance(Stream).
        /// </summary>
        public static readonly int DefaultBufferSize = 4096;

        private byte[] buffer;
        private int position;
        private int? savedPosition;

        #region Construction
        public CodedOutputStream() : this(new byte[DefaultBufferSize])
        {
        }
        /// <summary>
        /// Creates a new CodedOutputStream that writes directly to the given
        /// byte array. If more bytes are written than fit in the array,
        /// OutOfSpaceException will be thrown.
        /// </summary>
        public CodedOutputStream(byte[] buffer)
        {
            this.buffer = buffer;
        }
        #endregion

        public byte[] Buffer { get {  return buffer; } }

        /// <summary>
        /// Returns the current position in the stream, or the position in the output buffer
        /// </summary>
        public int Position
        {
            get { return position; }
        }

        public void Reset()
        {
            position = 0;
            savedPosition = null;
        }

        public void SavePosition()
        {
            savedPosition = position;
        }

        public bool TryWriteBoolAtSavedPosition(bool value)
        {
            if (savedPosition == null)
                return false;

            buffer[savedPosition.Value] = value ? (byte)1 : (byte)0;
            return true;
        }

        #region Writing of values (not including tags)

        /// <summary>
        /// Writes a double field value, without a tag, to the stream.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void WriteDouble(double value)
        {
            WriteRawLittleEndian64((ulong)BitConverter.DoubleToInt64Bits(value));
        }

        /// <summary>
        /// Writes a float field value, without a tag, to the stream.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void WriteFloat(float value)
        {
            byte[] rawBytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
            {
                ByteArray.Reverse(rawBytes);
            }

            if (buffer.Length - position >= 4)
            {
                buffer[position++] = rawBytes[0];
                buffer[position++] = rawBytes[1];
                buffer[position++] = rawBytes[2];
                buffer[position++] = rawBytes[3];
            }
            else
            {
                WriteRawBytes(rawBytes, 0, 4);
            }
        }

        /// <summary>
        /// Writes a uint64 field value, without a tag, to the stream.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void WriteUInt64(ulong value)
        {
            WriteRawVarint64(value);
        }

        /// <summary>
        /// Writes an int64 field value, without a tag, to the stream.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void WriteInt64(long value)
        {
            WriteRawVarint64((ulong) value);
        }

        /// <summary>
        /// Writes an int32 field value, without a tag, to the stream.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void WriteInt32(int value)
        {
            if (value >= 0)
            {
                WriteRawVarint32((uint) value);
            }
            else
            {
                // Must sign-extend.
                WriteRawVarint64((ulong) value);
            }
        }

        /// <summary>
        /// Writes a fixed64 field value, without a tag, to the stream.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void WriteFixed64(ulong value)
        {
            WriteRawLittleEndian64(value);
        }

        /// <summary>
        /// Writes a fixed32 field value, without a tag, to the stream.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void WriteFixed32(uint value)
        {
            WriteRawLittleEndian32(value);
        }

        /// <summary>
        /// Writes a bool field value, without a tag, to the stream.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void WriteBool(bool value)
        {
            WriteRawByte(value ? (byte) 1 : (byte) 0);
        }

        /// <summary>
        /// Writes a string field value, without a tag, to the stream.
        /// The data is length-prefixed.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void WriteString(string value)
        {
            int length = Utf8Encoding.GetByteCount(value);
            WriteString(value, length);
        }

        /// <summary>
        /// Writes a string field value, without a tag, to the stream.
        /// The data is length-prefixed.
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <param name="length"></param>
        public void WriteString(string value, int length)
        {
            // Optimise the case where we have enough space to write
            // the string directly to the buffer, which should be common.
            WriteLength(length);
            if (buffer.Length - position >= length)
            {
                if (length == value.Length) // Must be all ASCII...
                {
                    for (int i = 0; i < length; i++)
                    {
                        buffer[position + i] = (byte)value[i];
                    }
                }
                else
                {
                    Utf8Encoding.GetBytes(value, 0, value.Length, buffer, position);
                }
                position += length;
            }
            else
            {
                byte[] bytes = Utf8Encoding.GetBytes(value);
                WriteRawBytes(bytes);
            }
        }

        /// <summary>
        /// Writes a uint32 value, without a tag, to the stream.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void WriteUInt32(uint value)
        {
            WriteRawVarint32(value);
        }

        /// <summary>
        /// Writes an enum value, without a tag, to the stream.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void WriteEnum(int value)
        {
            WriteInt32(value);
        }

        /// <summary>
        /// Writes an sfixed32 value, without a tag, to the stream.
        /// </summary>
        /// <param name="value">The value to write.</param>
        public void WriteSFixed32(int value)
        {
            WriteRawLittleEndian32((uint) value);
        }

        /// <summary>
        /// Writes an sfixed64 value, without a tag, to the stream.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void WriteSFixed64(long value)
        {
            WriteRawLittleEndian64((ulong) value);
        }

        /// <summary>
        /// Writes an sint32 value, without a tag, to the stream.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void WriteSInt32(int value)
        {
            WriteRawVarint32(EncodeZigZag32(value));
        }

        /// <summary>
        /// Writes an sint64 value, without a tag, to the stream.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void WriteSInt64(long value)
        {
            WriteRawVarint64(EncodeZigZag64(value));
        }

        /// <summary>
        /// Writes a length (in bytes) for length-delimited data.
        /// </summary>
        /// <remarks>
        /// This method simply writes a rawint, but exists for clarity in calling code.
        /// </remarks>
        /// <param name="length">Length value, in bytes.</param>
        public void WriteLength(int length)
        {
            WriteRawVarint32((uint) length);
        }

        #endregion

        #region Raw tag writing
        /// <summary>
        /// Encodes and writes a tag.
        /// </summary>
        /// <param name="fieldNumber">The number of the field to write the tag for</param>
        /// <param name="type">The wire format type of the tag to write</param>
        public void WriteTag(int fieldNumber, WireFormat.WireType type)
        {
            WriteRawVarint32(WireFormat.MakeTag(fieldNumber, type));
        }

        /// <summary>
        /// Writes an already-encoded tag.
        /// </summary>
        /// <param name="tag">The encoded tag</param>
        public void WriteTag(uint tag)
        {
            WriteRawVarint32(tag);
        }

        /// <summary>
        /// Writes the given single-byte tag directly to the stream.
        /// </summary>
        /// <param name="b1">The encoded tag</param>
        public void WriteRawTag(byte b1)
        {
            WriteRawByte(b1);
        }

        /// <summary>
        /// Writes the given two-byte tag directly to the stream.
        /// </summary>
        /// <param name="b1">The first byte of the encoded tag</param>
        /// <param name="b2">The second byte of the encoded tag</param>
        public void WriteRawTag(byte b1, byte b2)
        {
            WriteRawByte(b1);
            WriteRawByte(b2);
        }

        /// <summary>
        /// Writes the given three-byte tag directly to the stream.
        /// </summary>
        /// <param name="b1">The first byte of the encoded tag</param>
        /// <param name="b2">The second byte of the encoded tag</param>
        /// <param name="b3">The third byte of the encoded tag</param>
        public void WriteRawTag(byte b1, byte b2, byte b3)
        {
            WriteRawByte(b1);
            WriteRawByte(b2);
            WriteRawByte(b3);
        }

        /// <summary>
        /// Writes the given four-byte tag directly to the stream.
        /// </summary>
        /// <param name="b1">The first byte of the encoded tag</param>
        /// <param name="b2">The second byte of the encoded tag</param>
        /// <param name="b3">The third byte of the encoded tag</param>
        /// <param name="b4">The fourth byte of the encoded tag</param>
        public void WriteRawTag(byte b1, byte b2, byte b3, byte b4)
        {
            WriteRawByte(b1);
            WriteRawByte(b2);
            WriteRawByte(b3);
            WriteRawByte(b4);
        }

        /// <summary>
        /// Writes the given five-byte tag directly to the stream.
        /// </summary>
        /// <param name="b1">The first byte of the encoded tag</param>
        /// <param name="b2">The second byte of the encoded tag</param>
        /// <param name="b3">The third byte of the encoded tag</param>
        /// <param name="b4">The fourth byte of the encoded tag</param>
        /// <param name="b5">The fifth byte of the encoded tag</param>
        public void WriteRawTag(byte b1, byte b2, byte b3, byte b4, byte b5)
        {
            WriteRawByte(b1);
            WriteRawByte(b2);
            WriteRawByte(b3);
            WriteRawByte(b4);
            WriteRawByte(b5);
        }
        #endregion

        #region Underlying writing primitives
        /// <summary>
        /// Writes a 32 bit value as a varint. The fast route is taken when
        /// there's enough buffer space left to whizz through without checking
        /// for each byte; otherwise, we resort to calling WriteRawByte each time.
        /// </summary>
        internal void WriteRawVarint32(uint value)
        {
            // Optimize for the common case of a single byte value
            if (value < 128 && position < buffer.Length)
            {
                buffer[position++] = (byte)value;
                return;
            }

            while (value > 127 && position < buffer.Length)
            {
                buffer[position++] = (byte) ((value & 0x7F) | 0x80);
                value >>= 7;
            }
            while (value > 127)
            {
                WriteRawByte((byte) ((value & 0x7F) | 0x80));
                value >>= 7;
            }
            if (position < buffer.Length)
            {
                buffer[position++] = (byte) value;
            }
            else
            {
                WriteRawByte((byte) value);
            }
        }

        internal void WriteRawVarint64(ulong value)
        {
            while (value > 127 && position < buffer.Length)
            {
                buffer[position++] = (byte) ((value & 0x7F) | 0x80);
                value >>= 7;
            }
            while (value > 127)
            {
                WriteRawByte((byte) ((value & 0x7F) | 0x80));
                value >>= 7;
            }
            if (position < buffer.Length)
            {
                buffer[position++] = (byte) value;
            }
            else
            {
                WriteRawByte((byte) value);
            }
        }

        internal void WriteRawLittleEndian32(uint value)
        {
            if (position + 4 > buffer.Length)
            {
                WriteRawByte((byte) value);
                WriteRawByte((byte) (value >> 8));
                WriteRawByte((byte) (value >> 16));
                WriteRawByte((byte) (value >> 24));
            }
            else
            {
                buffer[position++] = ((byte) value);
                buffer[position++] = ((byte) (value >> 8));
                buffer[position++] = ((byte) (value >> 16));
                buffer[position++] = ((byte) (value >> 24));
            }
        }

        internal void WriteRawLittleEndian64(ulong value)
        {
            if (position + 8 > buffer.Length)
            {
                WriteRawByte((byte) value);
                WriteRawByte((byte) (value >> 8));
                WriteRawByte((byte) (value >> 16));
                WriteRawByte((byte) (value >> 24));
                WriteRawByte((byte) (value >> 32));
                WriteRawByte((byte) (value >> 40));
                WriteRawByte((byte) (value >> 48));
                WriteRawByte((byte) (value >> 56));
            }
            else
            {
                buffer[position++] = ((byte) value);
                buffer[position++] = ((byte) (value >> 8));
                buffer[position++] = ((byte) (value >> 16));
                buffer[position++] = ((byte) (value >> 24));
                buffer[position++] = ((byte) (value >> 32));
                buffer[position++] = ((byte) (value >> 40));
                buffer[position++] = ((byte) (value >> 48));
                buffer[position++] = ((byte) (value >> 56));
            }
        }

        internal void WriteRawByte(byte value)
        {
            EnsureCapacity(1);

            buffer[position++] = value;
        }

        internal void WriteRawByte(uint value)
        {
            WriteRawByte((byte) value);
        }

        public void WriteGuid(Guid value)
        {
            EnsureCapacity(GuidSize + 1);

            buffer[position++] = GuidSize; // length

            var blob = value.ToByteArray();
            buffer[position++] = 1 << 3 | 1; // tag
            for (var i = 0; i < 8; i++)
            {
                buffer[position++] = blob[i];
            }
            buffer[position++] = 2 << 3 | 1; // tag
            for (var i = 8; i < 16; i++)
            {
                buffer[position++] = blob[i];
            }
        }

        /// <summary>
        /// Writes out an array of bytes.
        /// </summary>
        internal void WriteRawBytes(byte[] value)
        {
            WriteRawBytes(value, 0, value.Length);
        }

        /// <summary>
        /// Writes out part of an array of bytes.
        /// </summary>
        internal void WriteRawBytes(byte[] value, int offset, int length)
        {
            EnsureCapacity(length);

            ByteArray.Copy(value, offset, buffer, position, length);
            // We have room in the current buffer.
            position += length;
        }

        public void WriteRawStream(Stream stream)
        {
            var length = (int)stream.Length;
            EnsureCapacity(length);

            stream.Position = 0;

            var memoryStream = stream as MemoryStream;
            if (memoryStream != null)
                position += memoryStream.Read(buffer, position, length);
            else
                WriteRawStreamSlow(stream);
        }

        private void WriteRawStreamSlow(Stream stream)
        {
            const int blockSize = 4096;

            while (true)
            {
                var readCount = stream.Read(buffer, position, blockSize);
                position += readCount;
                if (readCount != blockSize)
                    break;
            }
        }

        private void EnsureCapacity(int length)
        {
            if (buffer.Length - position >= length)
                return;

            var newBufferLength = Math.Max(position + length, buffer.Length * 2);
            var newBuffer = new byte[newBufferLength];
            ByteArray.Copy(buffer, 0, newBuffer, 0, buffer.Length);
            buffer = newBuffer;
        }

        #endregion

        /// <summary>
        /// Encode a 32-bit value with ZigZag encoding.
        /// </summary>
        /// <remarks>
        /// ZigZag encodes signed integers into values that can be efficiently
        /// encoded with varint.  (Otherwise, negative values must be 
        /// sign-extended to 64 bits to be varint encoded, thus always taking
        /// 10 bytes on the wire.)
        /// </remarks>
        internal static uint EncodeZigZag32(int n)
        {
            // Note:  the right-shift must be arithmetic
            return (uint) ((n << 1) ^ (n >> 31));
        }

        /// <summary>
        /// Encode a 64-bit value with ZigZag encoding.
        /// </summary>
        /// <remarks>
        /// ZigZag encodes signed integers into values that can be efficiently
        /// encoded with varint.  (Otherwise, negative values must be 
        /// sign-extended to 64 bits to be varint encoded, thus always taking
        /// 10 bytes on the wire.)
        /// </remarks>
        internal static ulong EncodeZigZag64(long n)
        {
            return (ulong) ((n << 1) ^ (n >> 63));
        }

        private void RefreshBuffer()
        {
            position = 0;
        }

        /// <summary>
        /// Indicates that a CodedOutputStream wrapping a flat byte array
        /// ran out of space.
        /// </summary>
        public sealed class OutOfSpaceException : IOException
        {
            internal OutOfSpaceException()
                : base("CodedOutputStream was writing to a flat byte array and ran out of space.")
            {
            }
        }

        /// <summary>
        /// Flushes any buffered data to the underlying stream (if there is one).
        /// </summary>
        public void Flush()
        {
            position = 0;
        }

        /// <summary>
        /// Verifies that SpaceLeft returns zero. It's common to create a byte array
        /// that is exactly big enough to hold a message, then write to it with
        /// a CodedOutputStream. Calling CheckNoSpaceLeft after writing verifies that
        /// the message was actually as big as expected, which can help bugs.
        /// </summary>
        public void CheckNoSpaceLeft()
        {
            if (SpaceLeft != 0)
            {
                throw new InvalidOperationException("Did not write as much data as expected.");
            }
        }

        /// <summary>
        /// If writing to a flat array, returns the space left in the array.
        /// </summary>
        public int SpaceLeft
        {
            get { return buffer.Length - position; }
        }
    }
}