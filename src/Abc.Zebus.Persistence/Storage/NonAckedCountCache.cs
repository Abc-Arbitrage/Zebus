using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Util.Extensions;

namespace Abc.Zebus.Persistence.Storage
{
    public class NonAckedCountCache
    {
        private readonly Dictionary<PeerId, NonAckedCount> _nonAckedCounts = new Dictionary<PeerId, NonAckedCount>();

        public IEnumerable<NonAckedCount> GetForUpdatedPeers(ICollection<(PeerId, int)> allPeerStates)
        {
            var updatedPeers = (from peerState in allPeerStates
                                let count = _nonAckedCounts.GetValueOrDefault(peerState.Item1, id => new NonAckedCount(id, -42))
                                where count.Count != peerState.Item2
                                select new NonAckedCount(peerState.Item1, peerState.Item2)).ToList();

            foreach (var peerState in allPeerStates)
            {
                _nonAckedCounts[peerState.Item1] = new NonAckedCount(peerState.Item1, peerState.Item2);
            }

            return updatedPeers;
        }
    }

    public readonly struct NonAckedCount
    {
        public readonly PeerId PeerId;
        public readonly int Count;

        public NonAckedCount(PeerId peerId, int count)
        {
            PeerId = peerId;
            Count = count;
        } 
    }
}
