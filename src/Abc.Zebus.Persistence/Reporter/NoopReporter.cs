using System.Collections.Generic;

namespace Abc.Zebus.Persistence.Reporter
{
    public class NoopReporter : IReporter
    {
        private static readonly List<ReplaySpeedReport> _emptyReplayReports = new(0);
        private static readonly List<StorageReport> _emptyStorageReports = new(0);

        public void AddReplaySpeedReport(ReplaySpeedReport replaySpeedReport)
        {
        }

        public IList<ReplaySpeedReport> TakeAndResetReplaySpeedReports()
        {
            return _emptyReplayReports;
        }

        public void AddStorageReport(StorageReport storageReport)
        {
        }

        public IList<StorageReport> TakeAndResetStorageReports()
        {
            return _emptyStorageReports;
        }
    }
}
