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
        }

        public PeerId PeerId { get; }

        public bool Removed { get; private set; }
        
        public long OldestNonAckedMessageTimestampInTicks { get; private set; }

        public long LastNonAckedMessageCountVersion { get; internal set; }

        public int NonAckedMessageCount { get; internal set; }

        public void UpdateOldestNonAckedMessageTimestamp(long uniqueTimestampInTicks)
        {
            OldestNonAckedMessageTimestampInTicks = uniqueTimestampInTicks;
        }

        public void MarkAsRemoved()
        {
            Removed = true;
        }
    }
}
