using System;
using System.Collections.Generic;

namespace Abc.Zebus.Persistence.Messages
{
    public static class BucketIdHelper
    {
        private static readonly TimeSpan _bucketSize = TimeSpan.FromHours(1);
        private static readonly long _ticksInABucket = _bucketSize.Ticks;

        public static long GetBucketId(DateTime timestamp)
        {
            return GetBucketId(timestamp.Ticks);
        }

        public static long GetBucketId(long timestampInTicks)
        {
            return timestampInTicks - timestampInTicks % _ticksInABucket;
        }

        public static long GetPreviousBucketId(long timestampInTicks)
        {
            return GetBucketId(timestampInTicks - _ticksInABucket);
        }

        public static IEnumerable<long> GetBucketsCollection(long beginTimestampInTicks)
        {
            return GetBucketsCollection(beginTimestampInTicks, DateTime.UtcNow);
        }

        public static IEnumerable<long> GetBucketsCollection(long beginTimestampInTicks, DateTime utcNow)
        {
            // A message could be received with a timestamp in the future from a machine with a clock-drift.
            // It is dangerous to stop scanning buckets using DateTime.UtcNow.
            // => Add _bucketSize to DateTime.UtcNow to scan one extra bucket.
            var endTimestampInTicks = utcNow.Add(_bucketSize).Ticks;

            return GetBucketsCollection(beginTimestampInTicks, endTimestampInTicks);
        }

        public static IEnumerable<long> GetBucketsCollection(long beginTimestampInTicks, long endTimestampInTicks)
        {
            var latestBucketId = GetBucketId(endTimestampInTicks);

            var currentBucket = GetBucketId(beginTimestampInTicks);
            while (currentBucket <= latestBucketId)
            {
                yield return currentBucket;
                currentBucket = GetBucketId(currentBucket + _ticksInABucket);
            }
        }
    }
}
