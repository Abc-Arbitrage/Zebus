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
            return _dataContext.StoragePeers
                               .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                               .Where(peer => peer.PeerId == peerId.ToString())
                               .Execute()
                               .FirstOrDefault()
                               .ToPeerDescriptor();
        }

        public IEnumerable<PeerDescriptor> GetPeers()
        {
            return _dataContext.StoragePeers
                               .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                               .Execute()
                               .Select(peer => peer.ToPeerDescriptor())
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
            _dataContext.StoragePeers
                        .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                        .Where(peer => peer.PeerId == peerId.ToString())
                        .Delete()
                        .SetTimestamp(DateTime.UtcNow)
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

        public void AddDynamicSubscriptions(PeerId peerId, Subscription[] subscriptions)
        {
            throw new NotImplementedException();
        }

        public void RemoveDynamicSubscriptions(PeerId peerId, Subscription[] subscriptions)
        {
            throw new NotImplementedException();
        }
    }
}