using System;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Hosting;
using Abc.Zebus.Persistence.CQL.Data;
using Abc.Zebus.Persistence.CQL.Storage;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Persistence.Util;
using Cassandra.Data.Linq;

namespace Abc.Zebus.Persistence.CQL.PeriodicAction
{
    public class OldestNonAckedMessageUpdaterPeriodicAction : PeriodicActionHostInitializer
    {
        private readonly IPeerStateRepository _peerStateRepository;
        private readonly PersistenceCqlDataContext _dataContext;
        private DateTime _lastCheck;

        public OldestNonAckedMessageUpdaterPeriodicAction(IBus bus, IPeerStateRepository peerStateRepository, PersistenceCqlDataContext dataContext, ICqlPersistenceConfiguration configuration) 
            : base(bus, configuration.OldestMessagePerPeerCheckPeriod)
        {
            _peerStateRepository = peerStateRepository;
            _dataContext = dataContext;
        }

        public override void DoPeriodicAction()
        {
            var peersToCheck = _peerStateRepository.Where(x => x.LastNonAckedMessageCountChanged > _lastCheck).ToList();

            Parallel.ForEach(peersToCheck, new ParallelOptions { MaxDegreeOfParallelism = 10 }, UpdateOldestNonAckedMessage);
            
            _lastCheck = SystemDateTime.UtcNow;

            _peerStateRepository.Save();
        }

        private void UpdateOldestNonAckedMessage(PeerState peer)
        {
            var newOldest = GetOldestNonAckedMessageTimestamp(peer);

            if (newOldest == null)
                return;

            CleanBuckets(peer.PeerId, peer.OldestNonAckedMessageTimestampInTicks, newOldest.Value);
            peer.UpdateOldestNonAckedMessageTimestamp(newOldest.Value);
        }

        private long? GetOldestNonAckedMessageTimestamp(PeerState peer)
        {
            var peerId = peer.PeerId.ToString();
            var lastMessageTimestamp = 0L;
            foreach (var currentBucketId in BucketIdHelper.GetBucketsCollection(peer.OldestNonAckedMessageTimestampInTicks))
            {
                if (peer.HasBeenPurged)
                    return null;

                var messagesInBucket = _dataContext.PersistentMessages
                                                   .Where(x => x.PeerId == peerId
                                                               && x.BucketId == currentBucketId
                                                               && x.UniqueTimestampInTicks >= peer.OldestNonAckedMessageTimestampInTicks)
                                                   .OrderBy(x => x.UniqueTimestampInTicks)
                                                   .Select(x => new { x.IsAcked, x.UniqueTimestampInTicks })
                                                   .Execute();

                foreach (var message in messagesInBucket)
                {
                    lastMessageTimestamp = message.UniqueTimestampInTicks;
                    if (!message.IsAcked)
                    {
                        return lastMessageTimestamp;
                    }
                }
            }

            return lastMessageTimestamp == 0 ? SystemDateTime.UtcNow.Ticks : lastMessageTimestamp + 1;
        }

        private void CleanBuckets(PeerId peerId, long previousOldestMessageTimestamp, long newOldestMessageTimestamp)
        {
            var firstBucketToDelete = BucketIdHelper.GetBucketId(previousOldestMessageTimestamp);
            var lastBucketToDelete = BucketIdHelper.GetPreviousBucketId(newOldestMessageTimestamp);
            if (firstBucketToDelete == lastBucketToDelete)
                return;

            var bucketsToDelete = BucketIdHelper.GetBucketsCollection(firstBucketToDelete, lastBucketToDelete).ToArray();
            _dataContext.PersistentMessages
                        .Where(x => x.PeerId == peerId.ToString() && bucketsToDelete.Contains(x.BucketId))
                        .Delete()
                        .ExecuteAsync();
        }
    }
}