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
                                                       .Where(sub => sub.UselessKey == false && sub.PeerId == peerId.ToString())
                                                       .Execute()
                                                       .SelectMany(sub => sub.ToSubscriptionsForType().ToSubscriptions());

            return _dataContext.StoragePeers
                               .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                               .Where(peer => peer.UselessKey == false && peer.PeerId == peerId.ToString())
                               .Execute()
                               .FirstOrDefault()
                               .ToPeerDescriptor(peerDynamicSubscriptions);
        }

        public IEnumerable<PeerDescriptor> GetPeers(bool loadDynamicSubscriptions = true)
        {
            if (!loadDynamicSubscriptions)
            {
                return _dataContext.StoragePeers
                   .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                   .Where(peer => peer.UselessKey == false)
                   .Execute()
                   .Select(peer => peer.ToPeerDescriptor())
                   .ToList();
            }

            var dynamicSubscriptionsByPeer = _dataContext.DynamicSubscriptions
                                                         .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                                                         .Where(sub => sub.UselessKey == false)
                                                         .Execute()
                                                         .SelectMany(sub => sub.ToSubscriptionsForType().ToSubscriptions().Select(s => new { sub.PeerId, Subscription = s }))
                                                         .ToLookup(peerSub => peerSub.PeerId, peerSub=> peerSub.Subscription);
                                                         

            return _dataContext.StoragePeers
                               .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                               .Where(peer => peer.UselessKey == false)
                               .Execute()
                               .Select(peer => peer.ToPeerDescriptor(dynamicSubscriptionsByPeer[peer.PeerId]))
                               .Where(descriptor => descriptor != null)
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
                        .Where(peer => peer.UselessKey == false && peer.PeerId == peerId.ToString())
                        .Delete()
                        .SetTimestamp(now)
                        .Execute();
            _dataContext.DynamicSubscriptions
                        .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                        .Where(sub => sub.UselessKey == false && sub.PeerId == peerId.ToString())
                        .Delete()
                        .SetTimestamp(now)
                        .Execute();
        }

        public void SetPeerResponding(PeerId peerId, bool isResponding)
        {
            _dataContext.StoragePeers
                        .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                        .Where(peer => peer.UselessKey == false && peer.PeerId == peerId.ToString())
                        .Select(peer => new StoragePeer { IsResponding = isResponding })
                        .Update()
                        .SetTimestamp(DateTime.UtcNow)
                        .Execute();

        }

        public void AddDynamicSubscriptionsForTypes(PeerId peerId, DateTime timestampUtc, SubscriptionsForType[] subscriptionsForTypes)
        {
            if (subscriptionsForTypes == null)
                return;
            var batch = _dataContext.Session.CreateBatch();
            batch.SetConsistencyLevel(ConsistencyLevel.LocalQuorum);

            foreach (var subscription in subscriptionsForTypes)
            {
                batch.Append(_dataContext.DynamicSubscriptions
                                         .CreateInsert(subscription.ToStorageSubscription(peerId))
                                         .SetTimestamp(timestampUtc));
            }
            batch.Execute();
        }

        public void RemoveDynamicSubscriptionsForTypes(PeerId peerId, DateTime timestampUtc, MessageTypeId[] messageTypeIds)
        {
            if (messageTypeIds == null)
                return;
            var batch = _dataContext.Session.CreateBatch();
            batch.SetConsistencyLevel(ConsistencyLevel.LocalQuorum);

            foreach (var messageTypeId in messageTypeIds)
            {
                var deleteQuery = _dataContext.DynamicSubscriptions
                                              .Where(sub => sub.UselessKey == false && sub.PeerId == peerId.ToString() && sub.MessageTypeId == messageTypeId.FullName)
                                              .Delete()
                                              .SetTimestamp(timestampUtc);
                batch.Append(deleteQuery);
            }
            
            batch.Execute();
        }

        public void RemoveAllDynamicSubscriptionsForPeer(PeerId peerId, DateTime timestampUtc)
        {
            _dataContext.DynamicSubscriptions
                        .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                        .Where(sub => sub.UselessKey == false && sub.PeerId == peerId.ToString())
                        .Delete()
                        .SetTimestamp(timestampUtc)
                        .Execute();
        }
    }
}