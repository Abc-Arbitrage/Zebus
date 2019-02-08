using Abc.Zebus.Util;

namespace Abc.Zebus.Persistence.CQL.Storage
{
    public class PeerState
    {
        public PeerState(PeerId peerId, int nonAckMessageCount = 0, long oldestNonAckedMessageTimestamp = 0, bool removed = false)
        {
            PeerId = peerId;
            NonAckedMessageCount = nonAckMessageCount;
            OldestNonAckedMessageTimestampInTicks = oldestNonAckedMessageTimestamp > 0 ? oldestNonAckedMessageTimestamp : SystemDateTime.UtcNow.Ticks - CqlStorage.PersistentMessagesTimeToLive.Ticks;
            Removed = removed;
        }

        public PeerId PeerId { get; }

        public bool Removed { get; private set; }
        
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
