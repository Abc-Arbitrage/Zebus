using System.Collections.Generic;

namespace Abc.Zebus.Persistence.Reporter
{
    public interface IReporter
    {
        void AddReplaySpeedReport(int messagesReplayedCount, double sendDurationInSeconds, double ackDurationInSeconds);
        IList<ReplaySpeedReport> TakeAndResetReplaySpeedReports();

        void AddStorageReport(int messageCount, int batchSizeInBytes, int fattestMessageSizeInBytes, string fattestMessageTypeId);
        IList<StorageReport> TakeAndResetStorageReports();

    }
}