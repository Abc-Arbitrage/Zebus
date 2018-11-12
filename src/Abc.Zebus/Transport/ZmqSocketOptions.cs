using System;
using Abc.Zebus.Util;

namespace Abc.Zebus.Transport
{
    public class ZmqSocketOptions
    {
        public ZmqSocketOptions()
        {
            ReadTimeout = 300.Milliseconds();
            SendHighWaterMark = 20000;
            SendTimeout = 100.Milliseconds();
            SendRetriesBeforeSwitchingToClosedState = 2;
            ClosedStateDurationAfterSendFailure = 15.Seconds();
            ClosedStateDurationAfterConnectFailure = 2.Minutes();
            ReceiveHighWaterMark = 40000;
        }

        public TimeSpan ReadTimeout { set; get; }
        public int SendHighWaterMark { get; set; }
        public TimeSpan SendTimeout { get; set; }
        public int SendRetriesBeforeSwitchingToClosedState { get; set; }
        public TimeSpan ClosedStateDurationAfterSendFailure { get; set; }
        public TimeSpan ClosedStateDurationAfterConnectFailure { get; set; }
        public int ReceiveHighWaterMark { get; set; }
    }
}
