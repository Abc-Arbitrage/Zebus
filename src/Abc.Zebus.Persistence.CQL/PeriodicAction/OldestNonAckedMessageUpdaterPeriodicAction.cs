using System;
using System.Threading.Tasks;
using Abc.Zebus.Hosting;
using Abc.Zebus.Persistence.CQL.Storage;
using Abc.Zebus.Util;

namespace Abc.Zebus.Persistence.CQL.PeriodicAction
{
    public class OldestNonAckedMessageUpdaterPeriodicAction : PeriodicActionHostInitializer
    {
        private readonly IPeerStateRepository _peerStateRepository;
        private readonly ICqlPersistenceConfiguration _configuration;
        private readonly ICqlStorage _cqlStorage;
        private long _lastCheckVersion;
        private DateTime _lastGlobalCheck;

        public OldestNonAckedMessageUpdaterPeriodicAction(IBus bus, IPeerStateRepository peerStateRepository, ICqlPersistenceConfiguration configuration, ICqlStorage cqlStorage) 
            : base(bus, configuration.OldestMessagePerPeerCheckPeriod)
        {
            _peerStateRepository = peerStateRepository;
            _configuration = configuration;
            _cqlStorage = cqlStorage;
        }

        public override void DoPeriodicAction()
        {
            var isGlobalCheck = SystemDateTime.UtcNow >= _lastGlobalCheck.Add(_configuration.OldestMessagePerPeerGlobalCheckPeriod);
            if (isGlobalCheck)
            {
                _lastCheckVersion = 0;
                _lastGlobalCheck = SystemDateTime.UtcNow;
            }

            var peersToCheck = _peerStateRepository.GetUpdatedPeers(ref _lastCheckVersion);

            Parallel.ForEach(peersToCheck, new ParallelOptions { MaxDegreeOfParallelism = 10 }, UpdateOldestNonAckedMessage);

            _peerStateRepository.Save();
        }

        private void UpdateOldestNonAckedMessage(PeerState peer)
        {
            if (peer.Removed)
                return;

            var newOldest = _cqlStorage.GetOldestNonAckedMessageTimestamp(peer);

            _cqlStorage.CleanBuckets(peer.PeerId, peer.OldestNonAckedMessageTimestampInTicks, newOldest);

            peer.UpdateOldestNonAckedMessageTimestamp(newOldest);
        }
    }
}
