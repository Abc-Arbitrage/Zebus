using System;
using System.Linq;
using System.Threading;
using Abc.Zebus.Hosting;
using Abc.Zebus.Lotus;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using NCrunch.Framework;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Hosting
{
    [TestFixture]
    public class PeriodicActionHostInitializerTests
    {
        private XPeriodicActionHostInitializer _periodicInitializer;
        private TestBus _bus;

        [SetUp]
        public void SetUp()
        {
            _bus = new TestBus();

            _periodicInitializer = new XPeriodicActionHostInitializer(_bus, 20.Milliseconds());
        }

        [TearDown]
        public void Teardown()
        {
            _periodicInitializer.BeforeStop();
        }

        [Test]
        public void should_return_period()
        {
            _periodicInitializer.Period.ShouldEqual(20.Milliseconds());
        }

        [Test]
        public void should_call_the_periodic_action()
        {
            var callCount = 0;

            _periodicInitializer.PeriodicAction = () => callCount++;

            _periodicInitializer.AfterStart();

            Wait.Until(() => callCount >= 3, 1.Second());
        }

        [TestCase(5)]
        [TestCase(10)]
        public void should_pause_execution_after_successive_failures(int successiveFailureCount)
        {
            var callCount = 0;
            _periodicInitializer.PeriodicAction = () =>
            {
                callCount++;
                throw new Exception();
            };

            _periodicInitializer.ErrorCountBeforePause = successiveFailureCount;
            _periodicInitializer.AfterStart();

            Wait.Until(() => callCount >= successiveFailureCount, 1.Second());

            Thread.Sleep(200.Milliseconds());

            callCount.ShouldEqual(successiveFailureCount);
        }

        [Test]
        public void should_continue_execution_after_error_pause_duration()
        {
            var callCount = 0;
            _periodicInitializer.PeriodicAction = () =>
            {
                callCount++;
                throw new Exception();
            };

            _periodicInitializer.ErrorCountBeforePause = 2;
            _periodicInitializer.ErrorPauseDuration = 200.Milliseconds();

            _periodicInitializer.AfterStart();

            Wait.Until(() => callCount >= 2, 1.Second());

            Thread.Sleep(100.Milliseconds());
            callCount.ShouldEqual(2);

            Wait.Until(() => callCount > 2, 2.Seconds());
        }

        [Test]
        public void should_continue_execution_if_the_10_failures_are_not_consecutive()
        {
            var callCount = 0;
            _periodicInitializer.PeriodicAction = () =>
            {
                callCount++;
                if (callCount % 2 == 0)
                    throw new Exception();
            };

            _periodicInitializer.AfterStart();

            Wait.Until(() => callCount >= 21, 2.Seconds());
        }

        [Test]
        public void should_stop_the_loop_on_BeforeStop()
        {
            var callCount = 0;
            _periodicInitializer.PeriodicAction = () => callCount++;

            _periodicInitializer.AfterStart();
            Wait.Until(() => callCount >= 1, 2.Seconds());

            _periodicInitializer.BeforeStop();
            var callCountAfterStop = callCount;

            Thread.Sleep(100.Milliseconds());

            callCount.ShouldEqual(callCountAfterStop);
        }

        [Test]
        public void should_start_at_specified_time()
        {
            var offset = 1.Seconds();

            _periodicInitializer = new XPeriodicActionHostInitializer(_bus, 20.Milliseconds(), () => DateTime.UtcNow + offset);

            var signal = new ManualResetEvent(false);
            DateTime? firstCallTime = null;

            _periodicInitializer.PeriodicAction = () =>
            {
                if (firstCallTime == null)
                    firstCallTime = DateTime.UtcNow;

                signal.Set();
            };

            var startTime = DateTime.UtcNow;

            _periodicInitializer.AfterStart();

            signal.WaitOne(2.Seconds());

            firstCallTime.ShouldNotBeNull();
            firstCallTime.GetValueOrDefault().ShouldApproximateDateTime(startTime + offset, 50);
        }

        [Test]
        public void should_start_after_first_period()
        {
            var period = 500.Milliseconds();
            _periodicInitializer = new XPeriodicActionHostInitializer(_bus, period);

            var signal = new ManualResetEvent(false);
            DateTime? firstCallTime = null;

            _periodicInitializer.PeriodicAction = () =>
            {
                if (firstCallTime == null)
                    firstCallTime = DateTime.UtcNow;

                signal.Set();
            };

            var startTime = DateTime.UtcNow;

            _periodicInitializer.AfterStart();

            signal.WaitOne(2.Seconds());

            Console.WriteLine("First call " + firstCallTime.GetValueOrDefault().ToString("h:mm:ss.fffff"));
            Console.WriteLine("Expected " + (startTime + period).ToString("h:mm:ss.fffff"));

            firstCallTime.ShouldNotBeNull();
            firstCallTime.GetValueOrDefault().ShouldApproximateDateTime(startTime + period, 50);
        }

        [Test]
        public void should_not_run_action_in_parallel()
        {
            var runCount = 0;
            var wasRunInParallel = false;

            _periodicInitializer.PeriodicAction = () =>
            {
                if (!Monitor.TryEnter(_periodicInitializer))
                {
                    wasRunInParallel = true;
                    return;
                }

                if (runCount == 0)
                    Thread.Sleep(200);

                runCount++;

                Monitor.Exit(_periodicInitializer);
            };

            _periodicInitializer.AfterStart();

            Wait.Until(() => runCount >= 2, 10.Seconds());

            wasRunInParallel.ShouldBeFalse();
        }

        [Test, Serial]
        public void should_run_action_for_missed_timeouts()
        {
            var runCount = 0;

            _periodicInitializer.CatchupMode = PeriodicActionHostInitializer.MissedTimeoutCatchupMode.RunActionForMissedTimeouts;
            _periodicInitializer.PeriodicAction = () =>
            {
                if (runCount == 0)
                    Thread.Sleep(200);

                runCount++;
            };

            _periodicInitializer.AfterStart();

            Thread.Sleep(250);

            var minimumRunCount = 200/ 20;
            runCount.ShouldBeGreaterOrEqualThan(minimumRunCount);
        }

        [Test]
        public void should_skip_missed_timeouts()
        {
            var runCount = 0;

            _periodicInitializer.CatchupMode = PeriodicActionHostInitializer.MissedTimeoutCatchupMode.SkipMissedTimeouts;
            _periodicInitializer.PeriodicAction = () =>
            {
                if (runCount == 0)
                    Thread.Sleep(200);

                runCount++;
            };

            _periodicInitializer.AfterStart();

            Thread.Sleep(250);

            var maximumRunCount = 5; // 200 - 20 - 20 - 20 - 20
            runCount.ShouldBeLessOrEqualThan(maximumRunCount);
        }

        [Test]
        public void should_not_run_action_if_period_is_negative()
        {
            var period = -20.Milliseconds();
            _periodicInitializer = new XPeriodicActionHostInitializer(_bus, period);

            var callCount = 0;
            _periodicInitializer.PeriodicAction = () => callCount++;

            _periodicInitializer.AfterStart();

            Thread.Sleep(100);

            callCount.ShouldEqual(0);
        }

        [Test]
        public void should_publish_error()
        {
            _periodicInitializer.PeriodicAction = () => throw new Exception("Custom error");
            _periodicInitializer.ErrorCountBeforePause = 1;

            _periodicInitializer.AfterStart();

            Wait.Until(() => _bus.Events.Any(), 1.Second());

            var message = _bus.Events.OfType<CustomProcessingFailed>().ExpectedSingle();
            message.ExceptionMessage.ShouldContain("Custom error");
        }

        [Test]
        public void should_ignore_errors()
        {
            _periodicInitializer.PeriodicAction = () => throw new Exception("Custom error");
            _periodicInitializer.ErrorPublicationEnabled = false;

            _periodicInitializer.AfterStart();

            Thread.Sleep(200);

            _bus.ExpectNothing();
        }

        [Test]
        public void should_ignore_specific_errors()
        {
            var callCount = 0;
            _periodicInitializer.ShouldPublishErrorFunc = e => !(e is TimeoutException);
            _periodicInitializer.PeriodicAction = () =>
            {
                callCount++;
                if (callCount == 1)
                    throw new TimeoutException("Custom timeout");
               
                throw new Exception("Custom error");
            };

            _periodicInitializer.AfterStart();

            Wait.Until(() => _bus.Events.Any(), 1.Second());

            _bus.Events.OfType<CustomProcessingFailed>().ShouldNotContain(x => x.ExceptionMessage.Contains("timeout"));

        }

        private class XPeriodicActionHostInitializer : PeriodicActionHostInitializer
        {
            public XPeriodicActionHostInitializer(IBus bus, TimeSpan period, Func<DateTime> dueTimeUtcFunc = null)
                : base(bus, period, dueTimeUtcFunc)
            {
            }

            public Action PeriodicAction { get; set; }
            public Func<Exception, bool> ShouldPublishErrorFunc { get; set; }

            public override void DoPeriodicAction()
            {
                PeriodicAction?.Invoke();
            }

            protected override bool ShouldPublishError(Exception error)
            {
                return ShouldPublishErrorFunc != null ? ShouldPublishErrorFunc.Invoke(error) : base.ShouldPublishError(error);
            }
        }
    }
}
