using System;
using System.Threading;
using Abc.Zebus.Persistence.Util;
using Abc.Zebus.Testing.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.Tests.Util
{
    [TestFixture]
    public class SystemDateTimeTests
    {
        [SetUp]
        public void SetUp()
        {
            SystemDateTime.Reset();
        }

        [Test]
        public void should_be_equal_to_datetime_when_not_set_or_paused()
        {
            var dateTimeUtcNow = DateTime.UtcNow;
            var sysDateTimeUtcNow = SystemDateTime.UtcNow;
            var dateTimeNow = DateTime.Now;
            var sysDateTimeNow = SystemDateTime.Now;

            sysDateTimeUtcNow.Subtract(dateTimeUtcNow).ShouldBeLessOrEqualThan(5.Milliseconds());
            sysDateTimeNow.Subtract(dateTimeNow).ShouldBeLessOrEqualThan(5.Milliseconds());
        }

        [Test]
        public void should_pause_time()
        {
            using (SystemDateTime.PauseTime())
            {
                var utcNow = SystemDateTime.UtcNow;
                Thread.Sleep(50.Milliseconds());
                utcNow.ShouldEqual(SystemDateTime.UtcNow);
                utcNow.ShouldEqual(SystemDateTime.UtcNow);
                utcNow.ShouldEqual(SystemDateTime.UtcNow);

                var now = SystemDateTime.Now;
                Thread.Sleep(50.Milliseconds());
                now.ShouldEqual(SystemDateTime.Now);
                now.ShouldEqual(SystemDateTime.Now);
                now.ShouldEqual(SystemDateTime.Now);
            }
        }

        [Test]
        public void should_reset_time_when_paused()
        {
            using (SystemDateTime.PauseTime())
            {
                var now = SystemDateTime.UtcNow;
                Thread.Sleep(50.Milliseconds());
                SystemDateTime.Reset();
                Thread.Sleep(50.Milliseconds());

                SystemDateTime.UtcNow.ShouldBeGreaterThan(now);
                SystemDateTime.Now.ShouldBeGreaterThan(now);
            }
        }

        [Test]
        public void should_reset_time_when_set()
        {
            var fakeNow = new DateTime(1995, 1, 1, 1, 2, 3, 4, DateTimeKind.Local);
            using (SystemDateTime.Set(fakeNow))
            {
                SystemDateTime.Reset();

                SystemDateTime.UtcNow.ShouldBeGreaterThan(fakeNow);
                SystemDateTime.Now.ShouldBeGreaterThan(fakeNow);
            }
        }

        [Test]
        public void should_set_time_with_fake_local_time()
        {
            var fakeNow = new DateTime(1995, 1, 1, 1, 2, 3, 4, DateTimeKind.Local);
            using (SystemDateTime.Set(fakeNow))
            {
                SystemDateTime.Now.ShouldEqual(fakeNow);
                SystemDateTime.UtcNow.ShouldEqual(fakeNow.ToUniversalTime());
                SystemDateTime.Today.ShouldEqual(fakeNow.Date);
            }
        }

        [Test]
        public void should_set_time_with_fake_utc_time()
        {
            var fakeUtcNow = new DateTime(1995, 1, 1, 1, 2, 3, 4, DateTimeKind.Utc);
            using (SystemDateTime.Set(null, fakeUtcNow))
            {
                SystemDateTime.Now.ShouldEqual(fakeUtcNow.ToLocalTime());
                SystemDateTime.UtcNow.ShouldEqual(fakeUtcNow);
                SystemDateTime.Today.ShouldEqual(fakeUtcNow.Date);
            }
        }

        [Test]
        public void should_set_time_with_fake_local_and_utc_time()
        {
            var fakeNow = new DateTime(1995, 1, 1, 1, 2, 3, 4, DateTimeKind.Local);
            var fakeUtcNow = new DateTime(1995, 1, 1, 1, 2, 3, 4, DateTimeKind.Utc);
            using (SystemDateTime.Set(fakeNow, fakeUtcNow))
            {
                SystemDateTime.Now.ShouldEqual(fakeNow);
                SystemDateTime.UtcNow.ShouldEqual(fakeUtcNow);
                SystemDateTime.Today.ShouldEqual(fakeUtcNow.Date);
            }
        }

        [Test]
        public void should_throw_when_setting_nulls()
        {
            Assert.Throws<ArgumentNullException>(() => SystemDateTime.Set());
        }
    }
}