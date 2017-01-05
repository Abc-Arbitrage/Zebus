using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Abc.Zebus.Util
{
    internal class MutableMemoryStream : Stream
    {
        private const int _memStreamMaxLength = Int32.MaxValue;
        private int _origin;
        private byte[] _buffer;
        private int _capacity;
        private bool _expandable;
        private bool _isOpen;
        private int _length;
        private int _position;

        public virtual int Capacity
        {
            get
            {
                if (!_isOpen)
                    throw new InvalidOperationException();

                return _capacity - _origin;
            }
            set
            {
                if (value < Length)
                    throw new ArgumentOutOfRangeException(nameof(value));

                if (!_isOpen)
                    throw new InvalidOperationException();

                if (!_expandable && (value != Capacity))
                    throw new InvalidOperationException();

                if (!_expandable || value == _capacity)
                    return;

                if (value > 0)
                {
                    var newBuffer = new byte[value];
                    if (_length > 0)
                        System.Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _length);

                    _buffer = newBuffer;
                }
                else
                {
                    _buffer = null;
                }
                _capacity = value;
            }
        }

        public override bool CanRead => _isOpen;

        public override bool CanSeek => _isOpen;

        public override bool CanWrite => true;

        public override long Length
        {
            get
            {
                if (!_isOpen)
                    throw new InvalidOperationException();

                return _length - _origin;
            }
        }

        public override long Position
        {
            get
            {
                if (!_isOpen)
                    throw new InvalidOperationException();

                return _position - _origin;
            }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));

                if (!_isOpen)
                    throw new InvalidOperationException();

                if (value > _memStreamMaxLength)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _position = _origin + (int)value;
            }
        }

        public MutableMemoryStream()
            : this(0)
        {
        }

        public MutableMemoryStream(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _buffer = new byte[capacity];
            _capacity = capacity;
            _expandable = true;
            _origin = 0;
            _isOpen = true;
        }

        public override void Flush()
        {
        }

        public override int Read([In, Out] byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (buffer.Length - offset < count)
                throw new ArgumentException();

            if (!_isOpen)
                throw new InvalidOperationException();

            var n = _length - _position;
            if (n > count)
                n = count;

            if (n <= 0)
                return 0;

            if (n <= 8)
            {
                var byteCount = n;
                while (--byteCount >= 0)
                    buffer[offset + byteCount] = _buffer[_position + byteCount];
            }
            else
                System.Buffer.BlockCopy(_buffer, _position, buffer, offset, n);

            _position += n;

            return n;
        }

        public override int ReadByte()
        {
            if (!_isOpen)
                throw new InvalidOperationException();

            if (_position >= _length) return -1;

            return _buffer[_position++];
        }

        public override long Seek(long offset, SeekOrigin loc)
        {
            if (!_isOpen)
                throw new InvalidOperationException();

            if (offset > _memStreamMaxLength)
                throw new ArgumentOutOfRangeException(nameof(offset));

            switch (loc)
            {
                case SeekOrigin.Begin:
                    {
                        var tempPosition = unchecked(_origin + (int)offset);
                        if (offset < 0 || tempPosition < _origin)
                            throw new IOException();

                        _position = tempPosition;
                        break;
                    }
                case SeekOrigin.Current:
                    {
                        var tempPosition = unchecked(_position + (int)offset);
                        if (unchecked(_position + offset) < _origin || tempPosition < _origin)
                            throw new IOException();

                        _position = tempPosition;
                        break;
                    }
                case SeekOrigin.End:
                    {
                        var tempPosition = unchecked(_length + (int)offset);
                        if (unchecked(_length + offset) < _origin || tempPosition < _origin)
                            throw new IOException();

                        _position = tempPosition;
                        break;
                    }
                default:
                    throw new ArgumentException();
            }

            return _position;
        }

        public override void SetLength(long value)
        {
            if (value < 0 || value > Int32.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value));

            if (!CanWrite)
                throw new InvalidOperationException();

            if (value > (Int32.MaxValue - _origin))
                throw new ArgumentOutOfRangeException(nameof(value));

            var newLength = _origin + (int)value;
            var allocatedNewArray = EnsureCapacity(newLength);
            if (!allocatedNewArray && newLength > _length)
                Array.Clear(_buffer, _length, newLength - _length);

            _length = newLength;
            if (_position > newLength) _position = newLength;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (buffer.Length - offset < count)
                throw new ArgumentException();

            if (!_isOpen)
                throw new InvalidOperationException();

            if (!CanWrite)
                throw new InvalidOperationException();

            var i = _position + count;
            if (i < 0)
                throw new IOException();

            if (i > _length)
            {
                var mustZero = _position > _length;
                if (i > _capacity)
                {
                    var allocatedNewArray = EnsureCapacity(i);
                    if (allocatedNewArray)
                        mustZero = false;
                }
                if (mustZero)
                    Array.Clear(_buffer, _length, i - _length);
                _length = i;
            }
            if ((count <= 8) && (buffer != _buffer))
            {
                var byteCount = count;
                while (--byteCount >= 0)
                    _buffer[_position + byteCount] = buffer[offset + byteCount];
            }
            else
                System.Buffer.BlockCopy(buffer, offset, _buffer, _position, count);

            _position = i;
        }

        public override void WriteByte(byte value)
        {
            if (!_isOpen)
                throw new InvalidOperationException();

            if (!CanWrite)
                throw new InvalidOperationException();

            if (_position >= _length)
            {
                var newLength = _position + 1;
                var mustZero = _position > _length;
                if (newLength >= _capacity)
                {
                    var allocatedNewArray = EnsureCapacity(newLength);
                    if (allocatedNewArray)
                        mustZero = false;
                }
                if (mustZero)
                    Array.Clear(_buffer, _length, _position - _length);
                _length = newLength;
            }
            _buffer[_position++] = value;
        }

        public void SetBuffer(byte[] buffer, int index, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (buffer.Length - index < count)
                throw new ArgumentException();

            _buffer = buffer;
            _origin = _position = index;
            _length = _capacity = index + count;
            _expandable = false;
            _isOpen = true;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    _isOpen = false;
                    _expandable = false;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private bool EnsureCapacity(int value)
        {
            if (value < 0)
                throw new IOException();

            if (value > _capacity)
            {
                var newCapacity = value;
                if (newCapacity < 256)
                    newCapacity = 256;
                if (newCapacity < _capacity * 2)
                    newCapacity = _capacity * 2;
                Capacity = newCapacity;
                return true;
            }
            return false;
        }
    }
}