using System;
using System.Security.Cryptography;
using Abc.Zebus.Util;
using ProtoBuf;

namespace Abc.Zebus
{
    [ProtoContract]
    public struct MessageId : IEquatable<MessageId>
    {
        private static readonly DateTime _gregorianStart = new DateTime(1582, 10, 15, 0, 0, 0, DateTimeKind.Utc);
        private static readonly byte[] _randomBytes = new byte[6];
        private static readonly object _lock = new object();
        private static Guid? _pausedId;
        private static long _lastTimestampTicks;

        [ProtoMember(1, IsRequired = true)]
        public readonly Guid Value;

        static MessageId()
        {
            var random = new RNGCryptoServiceProvider();
            random.GetBytes(_randomBytes);
        }

        public MessageId(Guid value)
        {
            Value = value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public bool Equals(MessageId other)
        {
            return Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            return obj is MessageId && Equals((MessageId)obj);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(MessageId left, MessageId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MessageId left, MessageId right)
        {
            return !left.Equals(right);
        }

        public static IDisposable PauseIdGeneration()
        {
            _pausedId = CreateNewSequentialId();
            return new DisposableAction(() => _pausedId = null);
        }

        public static void ResetLastTimestamp()
        {
            _lastTimestampTicks = 0;
        }

        public static MessageId NextId()
        {
            var value = _pausedId ?? CreateNewSequentialId();
            return new MessageId(value);
        }

        private static Guid CreateNewSequentialId()
        {
            var timestampTicks = SystemDateTime.UtcNow.Ticks;
            timestampTicks = timestampTicks - _gregorianStart.Ticks;

            lock (_lock)
            {
                if (timestampTicks <= _lastTimestampTicks)
                    timestampTicks = _lastTimestampTicks + 1;

                _lastTimestampTicks = timestampTicks;
            }

            var newId = new byte[16];
            var offsetBytes = ConvertEndian(BitConverter.GetBytes((short)Environment.TickCount));
            var timestampBytes = ConvertEndian(BitConverter.GetBytes(timestampTicks));

            Array.Copy(_randomBytes, 0, newId, 10, _randomBytes.Length);
            Array.Copy(offsetBytes, 0, newId, 8, offsetBytes.Length);
            Array.Copy(timestampBytes, 4, newId, 0, 4);
            Array.Copy(timestampBytes, 2, newId, 4, 2);
            Array.Copy(timestampBytes, 0, newId, 6, 2);

            // set variant
            newId[8] &= 0x3f;
            newId[8] |= 0x80;

            // set version
            newId[6] &= 0x0f;
            newId[6] |= 0x01 << 4;

            // set node high order bit 1
            newId[10] |= 0x80;

            return new Guid(newId);
        }

        private static byte[] ConvertEndian(byte[] value)
        {
            if (!BitConverter.IsLittleEndian)
                return value;

            for (var index = 0; index < value.Length / 2; ++index)
            {
                var tmp = value[index];
                value[index] = value[value.Length - index - 1];
                value[value.Length - index - 1] = tmp;
            }

            return value;
        }

        private const long _num100NsIntervalsSinceUuidEpoch = 0x01b21dd213814000L;
        private const long _magicalOffset = 621355968000000000L;

        public DateTime GetDateTime()
        {
            var timestamp = GetJavaTicks(Value);
            return new DateTime(timestamp - _num100NsIntervalsSinceUuidEpoch + _magicalOffset, DateTimeKind.Utc);
        }

#pragma warning disable 675
        private static long GetJavaTicks(Guid uuid)
        {
            var bytes = uuid.ToByteArray();
            long mostSigBits = 0;
            for (int i = 0; i < 8; i++)
                mostSigBits = (mostSigBits << 8) | (bytes[i] & 0xff);

            return (mostSigBits & 0x0FFFL) << 48
                   | ((mostSigBits >> 16) & 0x0FFFFL) << 32
                   | (long)((ulong)mostSigBits >> 32);
        }
    }
}