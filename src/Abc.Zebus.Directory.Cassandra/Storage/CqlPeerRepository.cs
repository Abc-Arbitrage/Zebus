using Abc.Zebus.Directory.Cassandra.Cql;
using Abc.Zebus.Directory.Storage;
using Cassandra;
using Cassandra.Data.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Abc.Zebus.Directory.Cassandra.Storage
{
    public class CqlPeerRepository : IPeerRepository
    {
        private readonly DirectoryDataContext _dataContext;

        public CqlPeerRepository(DirectoryDataContext dataContext)
        {
            _dataContext = dataContext;
        }

        public PeerDescriptor Get(PeerId peerId)
        {
            var peerDynamicSubscriptions = _dataContext.DynamicSubscriptions
                                                       .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                                                       .Where(sub => sub.PeerId == peerId.ToString())
                                                       .Execute()
                                                       .Select(sub => sub.ToSubscription());

            return _dataContext.StoragePeers
                               .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                               .Where(peer => peer.PeerId == peerId.ToString())
                               .Execute()
                               .FirstOrDefault()
                               .ToPeerDescriptor(peerDynamicSubscriptions);
        }

        public IEnumerable<PeerDescriptor> GetPeers()
        {
            var dynamicSubscriptionsByPeer = _dataContext.DynamicSubscriptions
                                                         .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                                                         .Execute()
                                                         .ToLookup(sub => sub.PeerId, sub => sub.ToSubscription());
                                                         

            return _dataContext.StoragePeers
                               .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                               .Execute()
                               .Select(peer => peer.ToPeerDescriptor(dynamicSubscriptionsByPeer[peer.PeerId]))
                               .ToList();
        }

        public void AddOrUpdatePeer(PeerDescriptor peerDescriptor)
        {
            var storagePeer = peerDescriptor.ToStoragePeer();
            _dataContext.StoragePeers
                        .CreateInsert(storagePeer)
                        .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                        .SetTimestamp(storagePeer.TimestampUtc)
                        .Execute();
        }

        public void RemovePeer(PeerId peerId)
        {
            var now = DateTime.UtcNow;
            _dataContext.StoragePeers
                        .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                        .Where(peer => peer.PeerId == peerId.ToString())
                        .Delete()
                        .SetTimestamp(now)
                        .Execute();
            _dataContext.DynamicSubscriptions
                        .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                        .Where(sub => sub.PeerId == peerId.ToString())
                        .Delete()
                        .SetTimestamp(now)
                        .Execute();
        }

        public void SetPeerResponding(PeerId peerId, bool isResponding)
        {
            _dataContext.StoragePeers
                        .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                        .Where(peer => peer.PeerId == peerId.ToString())
                        .Select(peer => new StoragePeer { IsResponding = isResponding })
                        .Update()
                        .SetTimestamp(DateTime.UtcNow)
                        .Execute();

        }

        public void AddDynamicSubscriptions(PeerDescriptor peerDescriptor, Subscription[] subscriptions)
        {
            var batch = _dataContext.Session.CreateBatch();
            batch.SetConsistencyLevel(ConsistencyLevel.LocalQuorum);

            foreach (var subscription in subscriptions)
            {
                batch.Append(_dataContext.DynamicSubscriptions
                                         .CreateInsert(subscription.ToStorageSubscription(peerDescriptor.PeerId))
                                         .SetTimestamp(peerDescriptor.TimestampUtc ?? DateTime.UtcNow));
            }

            batch.Execute();
        }

        public void RemoveDynamicSubscriptions(PeerDescriptor peerDescriptor, Subscription[] subscriptions)
        {
            var batch = _dataContext.Session.CreateBatch();
            batch.SetConsistencyLevel(ConsistencyLevel.LocalQuorum);

            foreach (var subscriptionToRemove in subscriptions.Select(sub => sub.ToStorageSubscription(peerDescriptor.PeerId)))
            {
                var deleteQuery = _dataContext.DynamicSubscriptions
                                              .Where(sub => sub.PeerId == subscriptionToRemove.PeerId && sub.SubscriptionIdentifier == subscriptionToRemove.SubscriptionIdentifier)
                                              .Delete()
                                              .SetTimestamp(peerDescriptor.TimestampUtc ?? DateTime.UtcNow);
                batch.Append(deleteQuery);
            }
            
            batch.Execute();
        }
    }
}