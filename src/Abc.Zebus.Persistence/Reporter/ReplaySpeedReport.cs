namespace Abc.Zebus.Persistence.Reporter
{
    public class ReplaySpeedReport
    {
        public int MessageCount { get; }
        public double SendDurationInSeconds { get; }
        public double AckDurationInSeconds { get; }

        public ReplaySpeedReport(int messageCount, double sendDurationInSeconds, double ackDurationInSeconds)
        {
            MessageCount = messageCount;
            SendDurationInSeconds = sendDurationInSeconds;
            AckDurationInSeconds = ackDurationInSeconds;
        }
    }
}