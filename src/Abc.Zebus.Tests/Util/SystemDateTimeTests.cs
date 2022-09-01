using System;
using System.Threading;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Util
{
    [TestFixture]
    public class SystemDateTimeTests
    {
        [Test]
        public void should_be_equal_to_datetime_when_not_set_or_paused()
        {
            var dateTimeUtcNow = DateTime.UtcNow;
            var sysDateTimeUtcNow = SystemDateTime.UtcNow;
            sysDateTimeUtcNow.Subtract(dateTimeUtcNow).ShouldBeLessOrEqualThan(50.Milliseconds());
        }

        [Test]
        public void should_pause_time()
        {
            using (SystemDateTime.PauseTime())
            {
                var utcNow = SystemDateTime.UtcNow;
                Thread.Sleep(2.Milliseconds());
                utcNow.ShouldEqual(SystemDateTime.UtcNow);
                utcNow.ShouldEqual(SystemDateTime.UtcNow);
                utcNow.ShouldEqual(SystemDateTime.UtcNow);
            }
        }

        [Test]
        public void should_reset_time_when_paused()
        {
            using (SystemDateTime.PauseTime())
            {
                var utcNow = SystemDateTime.UtcNow;
                Thread.Sleep(2.Milliseconds());
                SystemDateTime.Reset();
                Thread.Sleep(2.Milliseconds());

                SystemDateTime.UtcNow.ShouldBeGreaterThan(utcNow);
            }
        }

        [Test]
        public void should_reset_time_when_set()
        {
            var fakeUtcNow = new DateTime(1995, 1, 1, 1, 2, 3, 4, DateTimeKind.Utc);
            using (SystemDateTime.PauseTime(fakeUtcNow))
            {
                SystemDateTime.Reset();

                SystemDateTime.UtcNow.ShouldBeGreaterThan(fakeUtcNow);
            }
        }

        [Test]
        public void should_set_time_with_fake_utc_time()
        {
            var fakeUtcNow = new DateTime(1995, 1, 1, 1, 2, 3, 4, DateTimeKind.Utc);
            using (SystemDateTime.PauseTime(fakeUtcNow))
            {
                SystemDateTime.UtcNow.ShouldEqual(fakeUtcNow);
            }
        }
    }
}
