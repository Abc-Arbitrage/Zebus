namespace Abc.Zebus.Persistence
{
    public class ReplayDurationInfo
    {
        public int MessageCount { get; }
        public double SendDurationInSeconds { get; }
        public double AckDurationInSeconds { get; }

        public ReplayDurationInfo(int messageCount, double sendDurationInSeconds, double ackDurationInSeconds)
        {
            MessageCount = messageCount;
            SendDurationInSeconds = sendDurationInSeconds;
            AckDurationInSeconds = ackDurationInSeconds;
        }
    }
}