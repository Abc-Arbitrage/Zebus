using System;
using System.IO;
using Abc.Zebus.Util.Annotations;

namespace Abc.Zebus.Util
{
    internal struct Buffer
    {
        private byte[] _data;
        private int _length;

        public Buffer(byte[] data) :
            this(data, data.Length)
        {
        }

        public Buffer(byte[] data, int length)
        {
            _data = data;
            _length = length;
        }

        public Buffer(int byteCount)
        {
            _data = new byte[byteCount];
            _length = 0;
        }

        public byte[] Data
        {
            get { return _data; }
            private set { _data = value; }
        }

        public int Length
        {
            get { return _length; }
            set
            {
                if(value < 0 || value > _data.Length)
                    throw new ArgumentOutOfRangeException("Length");
                _length = value;
            }
        }

        public byte[] ToByteArray()
        {
            var data = new Byte[_length];
            System.Buffer.BlockCopy(_data, 0, data, 0, _length);
            return data; 
        }

        public void CopyTo(ref Buffer buffer)
        {
            buffer._length = _length;
            System.Buffer.BlockCopy(_data, 0, buffer._data, 0, _length);
        }

        public void CopyFrom(ref Buffer buffer)
        {
            _length = buffer._length;
            System.Buffer.BlockCopy(buffer._data, 0, _data, 0, _length);
        }
        
        public void CopyFrom([NotNull] byte[] bytes)
        {
            if (bytes == null) 
                throw new ArgumentNullException(nameof(bytes));
            
            _length = bytes.Length;
            System.Buffer.BlockCopy(bytes, 0, _data, 0, _length);
        }
        
        public Stream GetStream()
        {
            return new MemoryStream(_data, 0, _length);
        }
        
        public void Reset()
        {
            _length = 0;
        }
        
        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(_length);
            writer.Write(_data, 0, _length);
        }

        public void ReadFrom(BinaryReader binaryReader)
        {
            var length = binaryReader.ReadInt32();
            binaryReader.Read(_data, 0, length);
            _length = length;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                const int p = 16777619;
                int hash = (int)2166136261;

                for (int i = 0; i < _length; i++)
                    hash = (hash ^ _data[i]) * p;

                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                return hash;
            }
        }

        public void CopyFrom(byte[] buffer, int offset, int length)
        {
            _length = length;
            System.Buffer.BlockCopy(buffer, offset, _data, 0, _length);
        }
    }
}