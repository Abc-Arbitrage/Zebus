using System.Collections.Generic;

namespace Abc.Zebus.Persistence.Storage
{
    public class NonAckedCountCache
    {
        private readonly Dictionary<PeerId, NonAckedCount> _nonAckedCounts = new Dictionary<PeerId, NonAckedCount>();

        public IEnumerable<NonAckedCount> GetUpdatedValues(IEnumerable<NonAckedCount> allNonAckedCounts)
        {
            var updatedPeers = new List<NonAckedCount>();

            foreach (var nonAckedCount in allNonAckedCounts)
            {
                if (_nonAckedCounts.TryGetValue(nonAckedCount.PeerId, out var previousNonAckedCount) && previousNonAckedCount.Count == nonAckedCount.Count)
                    continue;

                _nonAckedCounts[nonAckedCount.PeerId] = nonAckedCount;
                updatedPeers.Add(nonAckedCount);
            }

            return updatedPeers;
        }
    }
}
