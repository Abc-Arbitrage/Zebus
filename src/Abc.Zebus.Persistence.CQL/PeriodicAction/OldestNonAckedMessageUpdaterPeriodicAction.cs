using System;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Hosting;
using Abc.Zebus.Persistence.CQL.Storage;
using Abc.Zebus.Persistence.Storage;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Extensions;

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
            var isGlobalCheck = ShouldPerformGlobalCheck();
            var peers = _cqlStorage.GetAllKnownPeers().AsList();
            var updatedNonAckedCounts = _nonAckedCountCache.Update(peers.Select(x => new NonAckedCount(x.PeerId, x.NonAckedMessageCount)));
            var updatedPeerIds = updatedNonAckedCounts.Select(x => x.PeerId).ToHashSet();
            var peersToCheck = isGlobalCheck ? peers : peers.Where(x => updatedPeerIds.Contains(x.PeerId));

            if (isGlobalCheck)
                _lastGlobalCheck = SystemDateTime.UtcNow;

            Parallel.ForEach(peersToCheck, new ParallelOptions { MaxDegreeOfParallelism = 10 }, UpdateOldestNonAckedMessage);
        }

        private bool ShouldPerformGlobalCheck()
        {
            return SystemDateTime.UtcNow >= _lastGlobalCheck.Add(_configuration.OldestMessagePerPeerGlobalCheckPeriod);
        }

        private void UpdateOldestNonAckedMessage(PeerState peer)
        {
            if (peer.Removed)
                return;

            try
            {
                _cqlStorage.UpdateNewOldestMessageTimestamp(peer)
                           .Wait(_configuration.OldestMessagePerPeerCheckPeriod);
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to update oldest message timestamp for peer {peer.PeerId}", ex);
            }
        }
    }
}
