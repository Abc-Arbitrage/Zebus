using System;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Util
{
    [TestFixture]
    public class UniqueTimestampProviderTests
    {
        [Test]
        public void should_always_return_different_timestamps()
        {
            var provider = new UniqueTimestampProvider();
            var previousTimestamp = DateTime.MinValue;
            for (var i = 0; i < 1000; ++i)
            {
                var timestamp = provider.NextUtcTimestamp();
                timestamp.ShouldNotEqual(previousTimestamp);
                previousTimestamp = timestamp;
            }
        }

        [Test]
        public void should_return_utc_timestamps()
        {
            using (SystemDateTime.PauseTime())
            {
                new UniqueTimestampProvider().NextUtcTimestamp().ShouldApproximateDateTime(SystemDateTime.UtcNow);
            }
        }
    }
}