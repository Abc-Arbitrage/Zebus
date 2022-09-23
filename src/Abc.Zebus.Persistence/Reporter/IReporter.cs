using System.Collections.Generic;

namespace Abc.Zebus.Persistence.Reporter
{
    public interface IReporter
    {
        void AddReplaySpeedReport(ReplaySpeedReport replaySpeedReport);
        IList<ReplaySpeedReport> TakeAndResetReplaySpeedReports();

        void AddStorageReport(StorageReport storageReport);
        IList<StorageReport> TakeAndResetStorageReports();
    }
}
