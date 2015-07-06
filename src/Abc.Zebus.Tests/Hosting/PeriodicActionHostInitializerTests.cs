using System;
using System.Threading;
using Abc.Zebus.Hosting;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Hosting
{
    [TestFixture]
    public class PeriodicActionHostInitializerTests
    {
        private Mock<PeriodicActionHostInitializer> _periodicInitializer;

        [SetUp]
        public void SetUp()
        {
            _periodicInitializer = new Mock<PeriodicActionHostInitializer>(new TestBus(), 20.Milliseconds(), null) { CallBase = true };
        }

        [TearDown]
        public void Teardown()
        {
            _periodicInitializer.Object.BeforeStop();
        }

        [Test]
        public void should_return_period()
        {
            _periodicInitializer.Object.Period.ShouldEqual(20.Milliseconds());
        }

        [Test]
        public void should_call_the_periodic_action()
        {
            var callCount = 0;
            _periodicInitializer.Setup(x => x.DoPeriodicAction()).Callback(() => ++callCount);
            _periodicInitializer.Object.AfterStart();

            Wait.Until(() => callCount >= 3, 1.Second());
        }

        [TestCase(5)]
        [TestCase(10)]
        public void should_pause_execution_after_successive_failures(int successiveFailureCount)
        {
            var callCount = 0;
            _periodicInitializer.Setup(x => x.DoPeriodicAction()).Callback(() => { ++callCount; throw new Exception(); });

            _periodicInitializer.Object.ErrorCountBeforePause = successiveFailureCount;
            _periodicInitializer.Object.AfterStart();
            Wait.Until(() => callCount >= successiveFailureCount, 1.Second());

            Thread.Sleep(200.Milliseconds());

            callCount.ShouldEqual(successiveFailureCount);
        }

        [Test]
        public void should_continue_execution_after_error_pause_duration()
        {
            var callCount = 0;
            _periodicInitializer.Setup(x => x.DoPeriodicAction()).Callback(() => { ++callCount; throw new Exception(); });

            _periodicInitializer.Object.ErrorCountBeforePause = 2;
            _periodicInitializer.Object.ErrorPauseDuration = 200.Milliseconds();
            _periodicInitializer.Object.AfterStart();
            Wait.Until(() => callCount >= 2, 1.Second());

            Thread.Sleep(100.Milliseconds());
            callCount.ShouldEqual(2);

            Wait.Until(() => callCount > 2, 500.Milliseconds());
        }

        [Test]
        public void should_continue_execution_if_the_10_failures_are_not_consecutive()
        {
            var callCount = 0;
            _periodicInitializer.Setup(x => x.DoPeriodicAction()).Callback(() => { ++callCount; if (callCount % 2 == 0)throw new Exception(); });
            _periodicInitializer.Object.AfterStart();

            Wait.Until(() => callCount >= 21, 2.Seconds());
        }

        [Test]
        public void should_stop_the_loop_on_BeforeStop()
        {
            var callCount = 0;
            _periodicInitializer.Setup(x => x.DoPeriodicAction()).Callback(() => ++callCount);

            _periodicInitializer.Object.AfterStart();
            Wait.Until(() => callCount >= 1, 2.Seconds());

            _periodicInitializer.Object.BeforeStop();
            var callCountAfterStop = callCount;

            Thread.Sleep(100.Milliseconds());

            callCount.ShouldEqual(callCountAfterStop);
        }

        [Test]
        public void should_start_at_specified_time()
        {
            var now = DateTime.UtcNow;
            var offset = 1.Seconds();
            var dueTime = now + offset;
            Func<DateTime> startOffsetFactory = () => dueTime;
            _periodicInitializer = new Mock<PeriodicActionHostInitializer>(new TestBus(), 20.Milliseconds(), startOffsetFactory) { CallBase = true };

            DateTime? firstCallTime = null;
            _periodicInitializer.Setup(x => x.DoPeriodicAction()).Callback(() =>
            {
                if (firstCallTime == null)
                    firstCallTime = DateTime.UtcNow;
            });

            _periodicInitializer.Object.AfterStart();
            
            Thread.Sleep((int)((offset.TotalMilliseconds) * 2));

            firstCallTime.ShouldNotBeNull();
            firstCallTime.Value.ShouldApproximateDateTime(dueTime, 50);
        }

        [Test]
        public void should_start_after_first_period()
        {
            var period = 500.Milliseconds();
            _periodicInitializer = new Mock<PeriodicActionHostInitializer>(new TestBus(), period, null) { CallBase = true };

            DateTime? firstCallTime = null;
            _periodicInitializer.Setup(x => x.DoPeriodicAction()).Callback(() =>
            {
                if (firstCallTime == null)
                    firstCallTime = DateTime.UtcNow;
            });

            _periodicInitializer.Object.AfterStart();
            var startTime = DateTime.UtcNow;

            Thread.Sleep((int)((period.TotalMilliseconds) * 2));

            firstCallTime.ShouldNotBeNull();
            Console.WriteLine("First call " + firstCallTime.Value.ToString("h:mm:ss.fffff"));
            Console.WriteLine("Expected " + (startTime + period).ToString("h:mm:ss.fffff"));
            firstCallTime.Value.ShouldApproximateDateTime(startTime + period, 50); 
        }
    }
}