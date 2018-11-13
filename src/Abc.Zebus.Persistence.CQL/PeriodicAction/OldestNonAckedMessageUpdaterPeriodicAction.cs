using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Hosting;
using Abc.Zebus.Persistence.CQL.Data;
using Abc.Zebus.Persistence.CQL.Storage;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Util;
using Cassandra.Data.Linq;

namespace Abc.Zebus.Persistence.CQL.PeriodicAction
{
    public class OldestNonAckedMessageUpdaterPeriodicAction : PeriodicActionHostInitializer
    {
        private readonly IPeerStateRepository _peerStateRepository;
        private readonly PersistenceCqlDataContext _dataContext;
        private readonly ICqlPersistenceConfiguration _configuration;
        private DateTime _lastCheck;
        private DateTime _lastGlobalCheck;

        public OldestNonAckedMessageUpdaterPeriodicAction(IBus bus, IPeerStateRepository peerStateRepository, PersistenceCqlDataContext dataContext, ICqlPersistenceConfiguration configuration) 
            : base(bus, configuration.OldestMessagePerPeerCheckPeriod)
        {
            _peerStateRepository = peerStateRepository;
            _dataContext = dataContext;
            _configuration = configuration;
        }

        public override void DoPeriodicAction()
        {
            var isGlobalCheck = SystemDateTime.UtcNow >= _lastGlobalCheck.Add(_configuration.OldestMessagePerPeerGlobalCheckPeriod);
            var peersToCheck = isGlobalCheck ? _peerStateRepository : _peerStateRepository.Where(x => x.LastNonAckedMessageCountChanged > _lastCheck);

            Parallel.ForEach(peersToCheck, new ParallelOptions { MaxDegreeOfParallelism = 10 }, UpdateOldestNonAckedMessage);

            _peerStateRepository.Save();

            _lastCheck = SystemDateTime.UtcNow;
            _lastGlobalCheck = isGlobalCheck ? SystemDateTime.UtcNow : _lastGlobalCheck;
        }

        private void UpdateOldestNonAckedMessage(PeerState peer)
        {
            if (peer.HasBeenPurged)
                return;

            var newOldest = GetOldestNonAckedMessageTimestamp(peer);

            CleanBuckets(peer.PeerId, peer.OldestNonAckedMessageTimestampInTicks, newOldest);

            peer.UpdateOldestNonAckedMessageTimestamp(newOldest);
        }

        private long GetOldestNonAckedMessageTimestamp(PeerState peer)
        {
            var peerId = peer.PeerId.ToString();
            var lastAckedMessageTimestamp = 0L;

            foreach (var currentBucketId in BucketIdHelper.GetBucketsCollection(peer.OldestNonAckedMessageTimestampInTicks))
            {
                var messagesInBucket = _dataContext.PersistentMessages
                                                   .Where(x => x.PeerId == peerId
                                                               && x.BucketId == currentBucketId
                                                               && x.UniqueTimestampInTicks >= peer.OldestNonAckedMessageTimestampInTicks)
                                                   .OrderBy(x => x.UniqueTimestampInTicks)
                                                   .Select(x => new { x.IsAcked, x.UniqueTimestampInTicks })
                                                   .Execute();

                foreach (var message in messagesInBucket)
                {
                    if (!message.IsAcked)
                        return message.UniqueTimestampInTicks;

                    lastAckedMessageTimestamp = message.UniqueTimestampInTicks;
                    
                }
            }

            return lastAckedMessageTimestamp == 0 ? SystemDateTime.UtcNow.Ticks : lastAckedMessageTimestamp + 1;
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
