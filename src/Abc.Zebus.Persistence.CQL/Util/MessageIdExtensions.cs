using System;

namespace Abc.Zebus.Persistence.CQL.Util
{
    public static class MessageIdExtensions
    {
        public static DateTime GetDateTimeForV2OrV3(this MessageId messageId)
        {
            var dateTime = messageId.GetDateTime();
            var isInvalidDateTime = dateTime.Year > DateTime.UtcNow.Year + 2;

            return isInvalidDateTime ? MessageIdV2.GetDateTime(messageId.Value) : dateTime;
        }

        private struct MessageIdV2
        {
            private static readonly long _ticksSinceEpoch = new DateTime(1582, 10, 15, 0, 0, 0, DateTimeKind.Utc).Ticks;

            public static DateTime GetDateTime(Guid uuid) => new DateTime(GetJavaTicks(uuid) + _ticksSinceEpoch, DateTimeKind.Utc);

#pragma warning disable 675
            private static long GetJavaTicks(Guid uuid)
            {
                var bytes = uuid.ToByteArray();
                var mostSigBits = 0L;
                for (var i = 0; i < 8; i++)
                {
                    mostSigBits = (mostSigBits << 8) | (bytes[i] & 0xff);
                }
                return (mostSigBits & 0x0FFFL) << 48 | ((mostSigBits >> 16) & 0x0FFFFL) << 32 | (long)((ulong)mostSigBits >> 32);
            }
#pragma warning restore 675
        }
    }
}
