using System;

namespace Abc.Zebus.Persistence.Reporter
{
    public interface IPersistenceReporter
    {
        void AddReplaySpeedReport(ReplaySpeedReport replaySpeedReport);
        void AddStorageReport(StorageReport storageReport);
        void AddStorageTime(TimeSpan elapsed);
    }
}
