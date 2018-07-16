using System;
using Abc.Zebus.Util;

namespace Abc.Zebus.Persistence.CQL.Storage
{
    public class PeerState
    {
        public PeerState(PeerId peerId, int nonAckMessageCount = 0, long oldestNonAckedMessageTimestamp = 0)
        {
            PeerId = peerId;
            NonAckedMessageCount = nonAckMessageCount;
            OldestNonAckedMessageTimestampInTicks = oldestNonAckedMessageTimestamp > 0 ? oldestNonAckedMessageTimestamp : SystemDateTime.UtcNow.Ticks - CqlStorage.PersistentMessagesTimeToLive.Ticks;
            LastNonAckedMessageCountChanged = SystemDateTime.UtcNow;
        }

        public PeerId PeerId { get; }

        public bool HasBeenPurged { get; private set; }
        
        public long OldestNonAckedMessageTimestampInTicks { get; private set; }

        public DateTime LastNonAckedMessageCountChanged { get; private set; }

        public int NonAckedMessageCount { get; private set; }

        public void UpdateNonAckedMessageCount(int delta)
        {
            LastNonAckedMessageCountChanged = SystemDateTime.UtcNow;
            NonAckedMessageCount += delta;
        }

        public void UpdateOldestNonAckedMessageTimestamp(long uniqueTimestampInTicks)
        {
            OldestNonAckedMessageTimestampInTicks = uniqueTimestampInTicks;
        }

        public void Purge()
        {
            HasBeenPurged = true;
        }
    }
}