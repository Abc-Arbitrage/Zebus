using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Directory.Cassandra.Data;
using Abc.Zebus.Directory.Storage;
using Cassandra;
using Cassandra.Data.Linq;

namespace Abc.Zebus.Directory.Cassandra.Storage
{
    public class CqlPeerRepository : IPeerRepository
    {
        private readonly DirectoryDataContext _dataContext;

        public CqlPeerRepository(DirectoryDataContext dataContext)
        {
            _dataContext = dataContext;
        }

        public bool? IsPersistent(PeerId peerId)
        {
            return _dataContext.Peers
                               .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                               .Where(peer => peer.PeerId == peerId.ToString())
                               .Select(x => (bool?)x.IsPersistent)
                               .Execute()
                               .FirstOrDefault();
        }

        public PeerDescriptor? Get(PeerId peerId)
        {
            var peerDynamicSubscriptions = _dataContext.DynamicSubscriptions
                                                       .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                                                       .Where(s => s.PeerId == peerId.ToString())
                                                       .Execute()
                                                       .SelectMany(s => s.ToSubscriptionsForType().ToSubscriptions());

            return _dataContext.Peers
                               .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                               .Where(peer => peer.PeerId == peerId.ToString())
                               .Execute()
                               .FirstOrDefault()
                               .ToPeerDescriptor(peerDynamicSubscriptions);
        }

        public IEnumerable<PeerDescriptor> GetPeers(bool loadDynamicSubscriptions = true)
        {
            if (!loadDynamicSubscriptions)
            {
                return _dataContext.Peers
                   .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                   .Execute()
                   .Select(peer => peer.ToPeerDescriptor()!)
                   .ToList();
            }

            var dynamicSubscriptionsByPeer = _dataContext.DynamicSubscriptions
                                                         .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                                                         .Execute()
                                                         .SelectMany(sub => sub.ToSubscriptionsForType().ToSubscriptions().Select(s => new { sub.PeerId, Subscription = s }))
                                                         .ToLookup(peerSub => peerSub.PeerId, peerSub=> peerSub.Subscription);


            return _dataContext.Peers
                               .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                               .Execute()
                               .Select(peer => peer.ToPeerDescriptor(dynamicSubscriptionsByPeer[peer.PeerId]))
                               .Where(descriptor => descriptor != null)
                               .ToList()!;
        }

        public void AddOrUpdatePeer(PeerDescriptor peerDescriptor)
        {
            var cassandraPeer = peerDescriptor.ToCassandra();
            _dataContext.Peers
                        .Insert(cassandraPeer)
                        .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                        .SetTimestamp(cassandraPeer.TimestampUtc)
                        .Execute();
        }

        public void RemovePeer(PeerId peerId)
        {
            var now = DateTime.UtcNow;

            _dataContext.Peers
                        .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                        .Where(peer => peer.PeerId == peerId.ToString())
                        .Delete()
                        .SetTimestamp(now)
                        .Execute();

            _dataContext.DynamicSubscriptions
                        .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                        .Where(s => s.PeerId == peerId.ToString())
                        .Delete()
                        .SetTimestamp(now)
                        .Execute();
        }

        public void SetPeerResponding(PeerId peerId, bool isResponding)
        {
            _dataContext.Peers
                        .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                        .Where(peer => peer.PeerId == peerId.ToString())
                        .Select(peer =>  new CassandraPeer { IsResponding = isResponding })
                        .Update()
                        .SetTimestamp(DateTime.UtcNow)
                        .Execute();

        }

        public void AddDynamicSubscriptionsForTypes(PeerId peerId, DateTime timestampUtc, SubscriptionsForType[] subscriptionsForTypes)
        {
            var batch = _dataContext.Session.CreateBatch();
            batch.SetConsistencyLevel(ConsistencyLevel.LocalQuorum);

            foreach (var subscription in subscriptionsForTypes)
            {
                batch.Append(_dataContext.DynamicSubscriptions
                                         .Insert(subscription.ToCassandra(peerId))
                                         .SetTimestamp(timestampUtc));
            }
            batch.Execute();
        }

        public void RemoveDynamicSubscriptionsForTypes(PeerId peerId, DateTime timestampUtc, MessageTypeId[] messageTypeIds)
        {
            var batch = _dataContext.Session.CreateBatch();
            batch.SetConsistencyLevel(ConsistencyLevel.LocalQuorum);

            foreach (var messageTypeId in messageTypeIds)
            {
                var deleteQuery = _dataContext.DynamicSubscriptions
                                              .Where(s => s.PeerId == peerId.ToString() && s.MessageTypeId == messageTypeId.FullName)
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
                        .Where(s => s.PeerId == peerId.ToString())
                        .Delete()
                        .SetTimestamp(timestampUtc)
                        .Execute();
        }
    }
}
