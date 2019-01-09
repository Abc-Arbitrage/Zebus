using Abc.Zebus.Persistence.Storage;

namespace Abc.Zebus.Persistence.CQL.Storage
{
    public interface ICqlStorage : IStorage
    {
        long GetOldestNonAckedMessageTimestamp(PeerState peer);
        void CleanBuckets(PeerId peerId, long previousOldestMessageTimestamp, long newOldestMessageTimestamp);
    }
}
