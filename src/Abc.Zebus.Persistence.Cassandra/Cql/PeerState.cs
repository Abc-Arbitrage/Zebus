using System;
using Abc.Zebus.Util;

namespace Abc.Zebus.Persistence.Cassandra.Cql
{
    public class PeerState
    {
        /// <summary>
        /// Because <see cref="OldestNonAckedMessageTimestampInTicks"/> is just an optimization to make replay faster,
        /// there is absolutely no risk in having its value set in the past. However, setting its value slightly in the
        /// future is quite dangerous because it can generate lost messages.
        ///
        /// This delay prevents <see cref="OldestNonAckedMessageTimestampInTicks"/> from being moved too aggressively.
        /// Its value is set to the maximum estimated clock-drift / network delay of the system.
        /// </summary>
        public static readonly TimeSpan OldestNonAckedMessageTimestampSafetyOffset = 20.Minutes();

        public static readonly TimeSpan MessagesTimeToLive = 30.Days();

        public PeerState(PeerId peerId, int nonAckMessageCount = 0, long oldestNonAckedMessageTimestamp = 0, bool removed = false)
        {
            PeerId = peerId;
            NonAckedMessageCount = nonAckMessageCount;
            OldestNonAckedMessageTimestampInTicks = oldestNonAckedMessageTimestamp > 0 ? oldestNonAckedMessageTimestamp : SystemDateTime.UtcNow.Ticks - MessagesTimeToLive.Ticks;
            Removed = removed;
        }

        public PeerId PeerId { get; }

        public bool Removed { get; private set; }

        /// <summary>
        /// Provides a timestamp that is lower than or equal to the timestamp of the last unacked message.
        ///
        /// The goal of this value is to  make replay efficient by using a recent starting point, thus reducing
        /// the number of messages that the replay will scan.
        /// </summary>
        public long OldestNonAckedMessageTimestampInTicks { get; }

        public int NonAckedMessageCount { get; }

        public void MarkAsRemoved()
        {
            Removed = true;
        }

        public PeerState WithNonAckedMessageCountDelta(int delta)
        {
            return new PeerState(PeerId, NonAckedMessageCount + delta, OldestNonAckedMessageTimestampInTicks, Removed);
        }

        public PeerState WithOldestNonAckedMessageTimestampInTicks(long value)
        {
            return new PeerState(PeerId, NonAckedMessageCount, value, Removed);
        }
    }
}
