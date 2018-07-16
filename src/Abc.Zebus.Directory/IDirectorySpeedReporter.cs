using System;
using System.Collections;
using System.Collections.Generic;

namespace Abc.Zebus.Directory
{
    public interface IDirectorySpeedReporter
    {
        void ReportRegistrationDuration(TimeSpan elapsed);
        void ReportUnregistrationDuration(TimeSpan elapsed);
        void ReportSubscriptionUpdateDuration(TimeSpan elaped);
        void ReportSubscriptionUpdateForTypesDuration(TimeSpan elapsed);

        IList<TimeSpan> GetAndResetRegistrationDurations();
        IList<TimeSpan> GetAndResetUnregistrationDurations();
        IList<TimeSpan> GetAndResetSubscriptionUpdateDurations();
        IList<TimeSpan> GetAndResetSubscriptionUpdateForTypesDurations();
    }
}