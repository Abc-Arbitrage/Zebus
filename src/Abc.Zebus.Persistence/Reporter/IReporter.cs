using System;

namespace Abc.Zebus.Persistence.Reporter
{
    public interface IReporter
    {
        void AddReplaySpeedReport(ReplaySpeedReport replaySpeedReport);
        void AddStorageReport(StorageReport storageReport);
        void AddStorageTime(TimeSpan elapsed);
    }
}
