using System;

namespace Abc.Zebus.Persistence.Reporter
{
    public class ReplaySpeedReport
    {
        public int MessageCount { get; }
        public TimeSpan SendDuration { get; }
        public TimeSpan AckDuration { get; }

        public ReplaySpeedReport(int messageCount, TimeSpan sendDuration, TimeSpan ackDuration)
        {
            MessageCount = messageCount;
            SendDuration = sendDuration;
            AckDuration = ackDuration;
        }
    }
}
