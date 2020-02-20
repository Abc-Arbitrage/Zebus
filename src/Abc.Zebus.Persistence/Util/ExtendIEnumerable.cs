using System.Collections.Generic;

namespace Abc.Zebus.Persistence.Util
{
    internal static class ExtendIEnumerable
    {
        private static IEnumerable<IReadOnlyCollection<T>> InnerPartition<T>(this IEnumerable<T> @this, int partitionSize)
        {
            var partition = new List<T>(partitionSize);
            foreach (var row in @this)
            {
                partition.Add(row);
                if (partition.Count != partitionSize)
                    continue;

                yield return partition;
                partition = new List<T>(partitionSize);
            }
            if (partition.Count != 0)
                yield return partition;
        }

        public static IEnumerable<IEnumerable<T>> Partition<T>(this IEnumerable<T> @this, int partitionSize, bool sharedEnumerator)
        {
            if (sharedEnumerator)
            {
                var partitioner = new Partitioner<T>(partitionSize);
                return partitioner.Partition(@this);
            }

            return InnerPartition(@this, partitionSize);
        }

        private class Partitioner<T>
        {
            private readonly int _partitionSize;
            private int _currentCount;

            private IEnumerator<T>? _enumerator;
            private bool _hasMoreItems;

            public Partitioner(int partitionSize)
            {
                _partitionSize = partitionSize;
            }

            public IEnumerable<IEnumerable<T>> Partition(IEnumerable<T> src)
            {
                _enumerator = src.GetEnumerator();
                _hasMoreItems = _enumerator.MoveNext();

                while (_hasMoreItems)
                    yield return GetConsecutives();
            }

            private IEnumerable<T> GetConsecutives()
            {
                _currentCount = 0;
                while (_hasMoreItems && _currentCount < _partitionSize)
                {
                    yield return _enumerator!.Current;
                    _hasMoreItems = _enumerator!.MoveNext();
                    _currentCount++;
                }
            }
        }
    }
}
