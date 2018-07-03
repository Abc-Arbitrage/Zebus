using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Abc.Zebus.Directory
{
    public class DirectorySpeedReporter : IDirectorySpeedReporter
    {
        private readonly ConcurrentStack<TimeSpan> _registrationDurations = new ConcurrentStack<TimeSpan>();
        private readonly ConcurrentStack<TimeSpan> _unregistrationDurations = new ConcurrentStack<TimeSpan>();
        private readonly ConcurrentStack<TimeSpan> _subscriptionUpdateDurations = new ConcurrentStack<TimeSpan>();
        private readonly ConcurrentStack<TimeSpan> _subscriptionUpdateForTypesDurations = new ConcurrentStack<TimeSpan>();

        public void ReportRegistrationDuration(TimeSpan elapsed)
        {
            _registrationDurations.Push(elapsed);
        }

        public void ReportUnregistrationDuration(TimeSpan elapsed)
        {
            _unregistrationDurations.Push(elapsed);
        }

        public void ReportSubscriptionUpdateDuration(TimeSpan elaped)
        {
            _subscriptionUpdateDurations.Push(elaped);
        }

        public void ReportSubscriptionUpdateForTypesDuration(TimeSpan elapsed)
        {
            _subscriptionUpdateForTypesDurations.Push(elapsed);
        }

        public IList<TimeSpan> GetAndResetRegistrationDurations()
        {
            return GetDurationsAndReset(_registrationDurations);
        }

        public IList<TimeSpan> GetAndResetUnregistrationDurations()
        {
            return GetDurationsAndReset(_unregistrationDurations);
        }

        public IList<TimeSpan> GetAndResetSubscriptionUpdateDurations()
        {
            return GetDurationsAndReset(_subscriptionUpdateDurations);
        }

        public IList<TimeSpan> GetAndResetSubscriptionUpdateForTypesDurations()
        {
            return GetDurationsAndReset(_subscriptionUpdateForTypesDurations);
        }

        private IList<TimeSpan> GetDurationsAndReset(ConcurrentStack<TimeSpan> durations)
        {
            var poppedItems = new TimeSpan[Math.Max(durations.Count, 10)];
            var poppedItemsCount = durations.TryPopRange(poppedItems, 0, poppedItems.Length);
            return poppedItems.Take(poppedItemsCount).Reverse().ToList();
        }
    }
}