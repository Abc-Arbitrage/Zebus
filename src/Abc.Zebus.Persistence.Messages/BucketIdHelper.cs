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

        public static IEnumerable<long> GetBucketsCollection(long beginTimestampInTicks, long endTimestampInTicks = 0)
        {
            var latestBucketId = GetBucketId(endTimestampInTicks > 0 ? endTimestampInTicks : DateTime.UtcNow.Ticks);

            var currentBucket = GetBucketId(beginTimestampInTicks);
            while (currentBucket <= latestBucketId)
            {
                yield return currentBucket;
                currentBucket = GetBucketId(currentBucket + _ticksInABucket);
            }
        }
    }
}