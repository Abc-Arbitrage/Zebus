using System;
using Abc.Zebus.Util;

namespace Abc.Zebus.Transport
{
    public class ZmqSocketOptions : IZmqSocketOptions
    {
        public ZmqSocketOptions()
        {
            ReadTimeout = 300.Milliseconds();
            SendHighWaterMark = 20000;
            SendTimeout = 100.Milliseconds();
            SendRetriesBeforeSwitchingToClosedState = 2;
            ClosedStateDuration = 15.Seconds();
            ReceiveHighWaterMark = 20000;
        }

        public TimeSpan ReadTimeout { set; get; }
        public int SendHighWaterMark { get; set; }
        public TimeSpan SendTimeout { get; set; }
        public int SendRetriesBeforeSwitchingToClosedState { get; set; }
        public TimeSpan ClosedStateDuration { get; set; }
        public int ReceiveHighWaterMark { get; set; }
    }
}