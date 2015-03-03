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
        [Test]
        public void should_call_the_periodic_action()
        {
            var callCount = 0;
            var periodicInitializer = new Mock<PeriodicActionHostInitializer>(new TestBus(), 20.Milliseconds()) { CallBase = true };
            periodicInitializer.Setup(x => x.DoPeriodicAction()).Callback(() => ++callCount);
            periodicInitializer.Object.AfterStart();

            Wait.Until(() => callCount >= 3, 1.Second());
        }

        [TestCase(5)]
        [TestCase(10)]
        public void should_pause_execution_after_successive_failures(int successiveFailureCount)
        {
            var callCount = 0;
            var periodicInitializer = new Mock<PeriodicActionHostInitializer>(new TestBus(), 20.Milliseconds()) { CallBase = true };
            periodicInitializer.Setup(x => x.DoPeriodicAction()).Callback(() => { ++callCount; throw new Exception(); });

            periodicInitializer.Object.ErrorCountBeforePause = successiveFailureCount;
            periodicInitializer.Object.AfterStart();
            Wait.Until(() => callCount >= successiveFailureCount, 1.Second());

            Thread.Sleep(200.Milliseconds());

            callCount.ShouldEqual(successiveFailureCount);
        }

        [Test]
        public void should_continue_execution_after_error_pause_duration()
        {
            var callCount = 0;
            var periodicInitializer = new Mock<PeriodicActionHostInitializer>(new TestBus(), 20.Milliseconds()) { CallBase = true };
            periodicInitializer.Setup(x => x.DoPeriodicAction()).Callback(() => { ++callCount; throw new Exception(); });

            periodicInitializer.Object.ErrorCountBeforePause = 2;
            periodicInitializer.Object.ErrorPauseDuration = 200.Milliseconds();
            periodicInitializer.Object.AfterStart();
            Wait.Until(() => callCount >= 2, 1.Second());

            Thread.Sleep(100.Milliseconds());
            callCount.ShouldEqual(2);

            Wait.Until(() => callCount > 2, 500.Milliseconds());
        }

        [Test]
        public void should_continue_execution_if_the_10_failures_are_not_consecutive()
        {
            var callCount = 0;
            var periodicInitializer = new Mock<PeriodicActionHostInitializer>(new TestBus(), 20.Milliseconds()) { CallBase = true };
            periodicInitializer.Setup(x => x.DoPeriodicAction()).Callback(() => { ++callCount; if (callCount % 2 == 0)throw new Exception(); });
            periodicInitializer.Object.AfterStart();

            Wait.Until(() => callCount >= 21, 2.Seconds());
        }

        [Test]
        public void should_stop_the_loop_on_BeforeStop()
        {
            var callCount = 0;
            var periodicInitializer = new Mock<PeriodicActionHostInitializer>(new TestBus(), 20.Milliseconds()) { CallBase = true };
            periodicInitializer.Setup(x => x.DoPeriodicAction()).Callback(() => ++callCount);

            periodicInitializer.Object.AfterStart();
            Wait.Until(() => callCount >= 1, 2.Seconds());

            periodicInitializer.Object.BeforeStop();
            var callCountAfterStop = callCount;

            Thread.Sleep(100.Milliseconds());

            callCount.ShouldEqual(callCountAfterStop);
        }
    }
}