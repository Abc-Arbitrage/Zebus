using System.Collections.Generic;

namespace Abc.Zebus.Persistence.Reporter
{
    public class NoopReporter : IReporter
    {
        private static readonly List<ReplaySpeedReport> _emptyReplayReports = new List<ReplaySpeedReport>(0);
        private static readonly List<StorageReport> _emptyStorageReports = new List<StorageReport>(0);

        public void AddReplaySpeedReport(int messagesReplayedCount, double sendDurationInSeconds, double ackDurationInSeconds)
        {
        }

        public IList<ReplaySpeedReport> TakeAndResetReplaySpeedReports()
        {
            return _emptyReplayReports;
        }

        public void AddStorageReport(int messageCount, int batchSizeInBytes, int fattestMessageSizeInBytes, string fattestMessageTypeId)
        {
        }

        public IList<StorageReport> TakeAndResetStorageReports()
        {
            return _emptyStorageReports;
        }
    }
}