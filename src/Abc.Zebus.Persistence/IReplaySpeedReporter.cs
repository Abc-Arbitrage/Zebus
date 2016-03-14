using System.Collections.Generic;

namespace Abc.Zebus.Persistence
{
    public interface IReplaySpeedReporter
    {
        void AddReplaySpeedInfo(int messagesReplayedCount, double sendDurationInSeconds, double ackDurationInSeconds);
        IList<ReplayDurationInfo> TakeAndResetReplayDurations();
    }
}