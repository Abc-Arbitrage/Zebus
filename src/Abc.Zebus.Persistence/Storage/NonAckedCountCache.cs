using System.Collections.Generic;

namespace Abc.Zebus.Persistence.Storage
{
    public class NonAckedCountCache
    {
        private readonly Dictionary<PeerId, int> _nonAckedCounts = new Dictionary<PeerId, int>();

        /// <summary>
        /// Update non-acked counts and return the collection of modified elements.
        /// </summary>
        public IEnumerable<NonAckedCount> Update(IEnumerable<NonAckedCount> nonAckedCounts)
        {
            foreach (var nonAckedCount in nonAckedCounts)
            {
                if (_nonAckedCounts.TryGetValue(nonAckedCount.PeerId, out var previousCount) && previousCount == nonAckedCount.Count)
                    continue;

                _nonAckedCounts[nonAckedCount.PeerId] = nonAckedCount.Count;

                yield return nonAckedCount;
            }
        }
    }
}
