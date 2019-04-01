using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Hosting;
using Abc.Zebus.Persistence.CQL.Storage;
using Abc.Zebus.Persistence.Storage;
using Abc.Zebus.Util;

namespace Abc.Zebus.Persistence.CQL.PeriodicAction
{
    public class OldestNonAckedMessageUpdaterPeriodicAction : PeriodicActionHostInitializer
    {
        private readonly ICqlPersistenceConfiguration _configuration;
        private readonly ICqlStorage _cqlStorage;
        private DateTime _lastGlobalCheck;
        private readonly NonAckedCountCache _nonAckedCountCache = new NonAckedCountCache();

        public OldestNonAckedMessageUpdaterPeriodicAction(IBus bus, ICqlPersistenceConfiguration configuration, ICqlStorage cqlStorage)
            : base(bus, configuration.OldestMessagePerPeerCheckPeriod)
        {
            _configuration = configuration;
            _cqlStorage = cqlStorage;
        }

        public override void DoPeriodicAction()
        {
            var isGlobalCheck = SystemDateTime.UtcNow >= _lastGlobalCheck.Add(_configuration.OldestMessagePerPeerGlobalCheckPeriod);
            var allPeersDictionary = _cqlStorage.GetAllKnownPeers().ToDictionary(state => state.PeerId);
            IEnumerable<PeerState> peersToCheck = allPeersDictionary.Values;
            var updatedPeers = _nonAckedCountCache.GetUpdatedValues(peersToCheck.Select(x => new NonAckedCount(x.PeerId, x.NonAckedMessageCount)));
            if (isGlobalCheck)
            {
                _lastGlobalCheck = SystemDateTime.UtcNow;
            }
            else
            {
                peersToCheck = updatedPeers.Select(x => allPeersDictionary[x.PeerId]);
            }

            Parallel.ForEach(peersToCheck, new ParallelOptions { MaxDegreeOfParallelism = 10 }, UpdateOldestNonAckedMessage);
        }

        private void UpdateOldestNonAckedMessage(PeerState peer)
        {
            if (peer.Removed)
                return;

            _cqlStorage.UpdateNewOldestMessageTimestamp(peer)
                       .Wait(_configuration.OldestMessagePerPeerCheckPeriod);
        }
    }
}
