using System;
using System.Security.Cryptography;

namespace Abc.Zebus.Persistence.CQL.Tests
{
    public static class MessageIdV2
    {
        private static readonly long _ticksSinceEpoch = new DateTime(1582, 10, 15, 0, 0, 0, DateTimeKind.Utc).Ticks;
        private static readonly byte[] _randomBytes = new byte[6];

        static MessageIdV2()
        {
            using (var random = new RNGCryptoServiceProvider())
            {
                random.GetBytes(_randomBytes);
            }
        }

        public static Guid CreateNewSequentialId(long timestampTicks)
        {
            var newId = new byte[16];
            var offsetBytes = ConvertEndian(BitConverter.GetBytes((short)Environment.TickCount));
            var timestampBytes = ConvertEndian(BitConverter.GetBytes(timestampTicks - _ticksSinceEpoch));

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
    }
}
