using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Directory.Tests
{
    public class DirectorySpeedReporterTests
    {
        private DirectorySpeedReporter _speedReporter;

        [SetUp]
        public void SetUp()
        {
            _speedReporter = new DirectorySpeedReporter();
        }

        public readonly ReportingMethod[] ReportingMethods =
        {
            new ReportingMethod { Name = "Registration", EnqueueMethod = (x, value) => x.ReportRegistrationDuration(value), RetrievalMethod = x => x.GetAndResetRegistrationDurations() },
            new ReportingMethod { Name = "Unregistration", EnqueueMethod = (x, value) => x.ReportUnregistrationDuration(value), RetrievalMethod = x => x.GetAndResetUnregistrationDurations() },
            new ReportingMethod { Name = "Subscription update", EnqueueMethod = (x, value) => x.ReportSubscriptionUpdateDuration(value), RetrievalMethod = x => x.GetAndResetSubscriptionUpdateDurations() },
            new ReportingMethod { Name = "Subscription update for types", EnqueueMethod = (x, value) => x.ReportSubscriptionUpdateForTypesDuration(value), RetrievalMethod = x => x.GetAndResetSubscriptionUpdateForTypesDurations() },
        };

        [Test]
        [TestCaseSource("ReportingMethods")]
        public void should_report_speed_for_given_method(ReportingMethod reportingMethod)
        {
            reportingMethod.EnqueueMethod(_speedReporter, 1.Second());
            reportingMethod.EnqueueMethod(_speedReporter, 2.Second());
            reportingMethod.EnqueueMethod(_speedReporter, 3.Second());

            reportingMethod.RetrievalMethod(_speedReporter).SequenceEqual(new[] { 1.Second(), 2.Second(), 3.Second() }).ShouldBeTrue();
            reportingMethod.RetrievalMethod(_speedReporter).ShouldBeEmpty();
        }

        public class ReportingMethod
        {
            public string Name { get; set; }
            public Action<IDirectorySpeedReporter, TimeSpan> EnqueueMethod { get; set; }
            public Func<IDirectorySpeedReporter, IList<TimeSpan>> RetrievalMethod { get; set; }

            public override string ToString()
            {
                return Name;
            }
        }
    }
}