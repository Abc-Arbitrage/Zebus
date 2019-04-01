using System;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Testing.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.CQL.Tests
{
    public class BucketIdHelperTests
    {
        [Test]
        public void should_return_correct_bucket_id()
        {
            var now = DateTime.UtcNow;
            var expectedBucketId = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).Ticks;

            BucketIdHelper.GetBucketId(now).ShouldEqual(expectedBucketId);
        }

        [Test]
        public void should_iterate_over_buckets_since_a_given_timestamp_and_stop_when_hitting_current_bucket()
        {
            var now = DateTime.UtcNow;
            var beginning = now.AddHours(-3);
            var expectedBucketIds = new[]
            {
                BucketIdHelper.GetBucketId(beginning),
                BucketIdHelper.GetBucketId(beginning.AddHours(1)),
                BucketIdHelper.GetBucketId(beginning.AddHours(2)),
                BucketIdHelper.GetBucketId(now),
                BucketIdHelper.GetBucketId(now.AddHours(1)),
            };

            BucketIdHelper.GetBucketsCollection(beginning.Ticks, now).ShouldBeEquivalentTo(expectedBucketIds);
        }

        [Test]
        public void should_iterate_over_buckets_since_a_given_timestamp_and_stop_when_hitting_latest_bucket_possible()
        {
            var now = DateTime.UtcNow;
            var beginning = now.AddHours(-3);
            var end = now.AddHours(1);
            var expectedBucketIds = new[]
            {
                BucketIdHelper.GetBucketId(beginning),
                BucketIdHelper.GetBucketId(beginning.AddHours(1)),
                BucketIdHelper.GetBucketId(beginning.AddHours(2)),
                BucketIdHelper.GetBucketId(now),
                BucketIdHelper.GetBucketId(end),
            };

            BucketIdHelper.GetBucketsCollection(beginning.Ticks, end.Ticks).ShouldBeEquivalentTo(expectedBucketIds);
        }

        [Test]
        public void should_not_iterate_over_future_buckets()
        {
            var now = DateTime.UtcNow;
            var beginning = now.AddHours(3);

            BucketIdHelper.GetBucketsCollection(beginning.Ticks).ShouldBeEmpty();
        }
    }
}
