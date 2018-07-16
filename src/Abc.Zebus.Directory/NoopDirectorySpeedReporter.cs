using System;
using System.Collections.Generic;
using System.Linq;

namespace Abc.Zebus.Directory
{
    public class NoopDirectorySpeedReporter : IDirectorySpeedReporter
    {
        public void ReportRegistrationDuration(TimeSpan elapsed)
        { 
        }

        public void ReportUnregistrationDuration(TimeSpan elapsed)
        {
        }

        public void ReportSubscriptionUpdateDuration(TimeSpan elaped)
        {
        }

        public void ReportSubscriptionUpdateForTypesDuration(TimeSpan elapsed)
        {
        }

        public IList<TimeSpan> GetAndResetRegistrationDurations()
        {
            return Enumerable.Empty<TimeSpan>().ToList();
        }

        public IList<TimeSpan> GetAndResetUnregistrationDurations()
        {
            return Enumerable.Empty<TimeSpan>().ToList();
        }

        public IList<TimeSpan> GetAndResetSubscriptionUpdateDurations()
        {
            return Enumerable.Empty<TimeSpan>().ToList();
        }

        public IList<TimeSpan> GetAndResetSubscriptionUpdateForTypesDurations()
        {
            return Enumerable.Empty<TimeSpan>().ToList();
        }
    }
}