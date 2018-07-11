using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Abc.Zebus.Util;
using ProtoBuf;

namespace Abc.Zebus
{
    [ProtoContract]
    public readonly struct MessageId : IEquatable<MessageId>
    {
        private static readonly TimeGuidGenerator _generator = new TimeGuidGenerator();
        private static Guid? _pausedGuid;

        [ProtoMember(1, IsRequired = true)]
        public readonly Guid Value;

        public MessageId(Guid value)
        {
            Value = value;
        }

        public override string ToString() => Value.ToString();

        public bool Equals(MessageId other) => Value.Equals(other.Value);

        public override bool Equals(object obj) => obj is MessageId id && Equals(id);

        public override int GetHashCode() => Value.GetHashCode();

        public static bool operator ==(MessageId left, MessageId right) => left.Equals(right);
        public static bool operator !=(MessageId left, MessageId right) => !left.Equals(right);

        public static IDisposable PauseIdGeneration()
        {
            lock (_generator)
            {
                _pausedGuid = NewGuid();
            }

            return new DisposableAction(() =>
            {
                lock (_generator)
                {
                    _pausedGuid = null;
                }
            });
        }

        public static IDisposable PauseIdGenerationAtDate(DateTime utcDatetime)
        {
            var systemDatetimeContext = SystemDateTime.Set(utcNow: utcDatetime);
            var messageIdContext = PauseIdGeneration();

            return new DisposableAction(() =>
            {
                messageIdContext.Dispose();
                systemDatetimeContext.Dispose();
            });
        }

        public static MessageId NextId()
        {
            lock (_generator)
            {
                return new MessageId(NewGuid());
            }
        }

        private static Guid NewGuid()
        {
            return _pausedGuid ?? _generator.NewGuid();
        }

        public DateTime GetDateTime()
        {
            return TimeGuidGenerator.ExtractDateTime(Value);
        }

        public static void ResetLastTimestamp()
        {
            lock (_generator)
            {
                _generator.Reset();
            }
        }

        /// <summary>
        /// Time-based Guid generator.
        /// Not thread-safe.
        /// </summary>
        /// <remarks>
        /// Reference: https://www.famkruithof.net/guid-uuid-timebased.html.
        /// </remarks>
        private class TimeGuidGenerator
        {
            private static readonly long _gregorianCalendarTimeTicks = new DateTime(1582, 10, 15, 0, 0, 0, DateTimeKind.Utc).Ticks;
            private static readonly RNGCryptoServiceProvider _cryptoServiceProvider = new RNGCryptoServiceProvider();

            private readonly uint _nodeIdPart1;
            private readonly ushort _nodeIdPart2;
            private readonly ushort _clockId;
            private long _lastTicks;

            public TimeGuidGenerator()
                : this(GetRandomNodeId(), GetRandomClockId())
            {
            }

            private TimeGuidGenerator(byte[] nodeId, ushort clockId)
            {
                _nodeIdPart1 = BitConverter.ToUInt32(nodeId, 0);
                _nodeIdPart2 = BitConverter.ToUInt16(nodeId, sizeof(int));

                // Variant Byte: 1.0.x
                // 10xxxxxx
                // turn off first 2 bits
                clockId &= 0xff3f; // 1111111100111111
                //turn on first bit
                clockId |= 0x0080; // 0000000010000000

                _clockId = clockId;
            }

            public void Reset()
            {
                _lastTicks = 0;
            }

            public Guid NewGuid()
            {
                var ticks = SystemDateTime.UtcNow.Ticks;
                _lastTicks = Math.Max(ticks, _lastTicks + 1);

                return NewGuid(_lastTicks);
            }

            private unsafe Guid NewGuid(long absoluteTimestamp)
            {
                var bytes = stackalloc byte[16];

                var relativeTimestamp = absoluteTimestamp - _gregorianCalendarTimeTicks;
                // 0-7: Timestamp
                *(long*)bytes = relativeTimestamp;

                // Version Byte: Time based
                // 0001xxxx for bytes[7]
                // turn off first 4 bits
                bytes[7] &= 0x0f; //00001111
                //turn on fifth bit
                bytes[7] |= 0x10; //00010000

                // 8-9: ClockId
                *(ushort*)(bytes + 8) = _clockId;

                // 10-15: NodeId
                *(uint*)(bytes + 10) = _nodeIdPart1;
                *(ushort*)(bytes + 14) = _nodeIdPart2;

                var a = (bytes[3] << 24) | (bytes[2] << 16) | (bytes[1] << 8) | bytes[0];
                var b = (short)((bytes[5] << 8) | bytes[4]);
                var c = (short)((bytes[7] << 8) | bytes[6]);

                return new Guid(a, b, c, bytes[8], bytes[9], bytes[10], bytes[11], bytes[12], bytes[13], bytes[14], bytes[15]);
            }

            private static byte[] GetRandomNodeId()
            {
                var nodeId = new byte[6];
                _cryptoServiceProvider.GetBytes(nodeId);

                return nodeId;
            }

            private static ushort GetRandomClockId()
            {
                var clockId = new byte[2];
                _cryptoServiceProvider.GetBytes(clockId);
                return BitConverter.ToUInt16(clockId, 0);
            }

            public static DateTime ExtractDateTime(Guid uuid)
            {
                var ticks = GetTicks(uuid) + _gregorianCalendarTimeTicks;
                return new DateTime(ticks, DateTimeKind.Utc);
            }

            private static unsafe long GetTicks(Guid uuid)
            {
                var proxy = (GuidProxy*)(&uuid);
                var timestamp = proxy->a;
                return timestamp & 0x0FFFFFFFFFFFFFFFL;
            }

            [StructLayout(LayoutKind.Sequential)]
            [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
            private struct GuidProxy
            {
                public long a;
            }
        }
    }
}
