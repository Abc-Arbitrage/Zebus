using System;
using Abc.Zebus.Util;

namespace Abc.Zebus.Transport
{
    public class ZmqSocketOptions
    {
        public ZmqSocketOptions()
        {
            SendHighWaterMark = 20000;
            SendTimeout = 100.Milliseconds();
            SendRetriesBeforeSwitchingToClosedState = 2;

            ClosedStateDurationAfterSendFailure = 15.Seconds();
            ClosedStateDurationAfterConnectFailure = 2.Minutes();

            ReceiveHighWaterMark = 40000;
            ReceiveTimeout = 300.Milliseconds();

            KeepAlive = KeepAliveOptions.On(30.Seconds(), 3.Seconds());
        }

        [Obsolete("Use ReceiveTimeout instead")]
        public TimeSpan ReadTimeout
        {
            get => ReceiveTimeout;
            set => ReceiveTimeout = value;
        }
        
        public int SendHighWaterMark { get; set; }
        public TimeSpan SendTimeout { get; set; }
        public int SendRetriesBeforeSwitchingToClosedState { get; set; }

        public TimeSpan ClosedStateDurationAfterSendFailure { get; set; }
        public TimeSpan ClosedStateDurationAfterConnectFailure { get; set; }

        public int ReceiveHighWaterMark { get; set; }
        public TimeSpan ReceiveTimeout { get; set; }

        public KeepAliveOptions KeepAlive { get; set; }

        public class KeepAliveOptions
        {
            public static KeepAliveOptions On(TimeSpan? keepAliveTimeout, TimeSpan? keepAliveInterval)
            {
                return new KeepAliveOptions
                {
                    Enabled = true,
                    KeepAliveTimeout = keepAliveTimeout,
                    KeepAliveInterval = keepAliveInterval,
                };
            }

            public static KeepAliveOptions Off()
            {
                return new KeepAliveOptions {Enabled = false };
            }

            private KeepAliveOptions()
            {
            }

            public bool Enabled { get; private set; }
            public TimeSpan? KeepAliveTimeout { get; private set; }
            public TimeSpan? KeepAliveInterval { get; private set; }
        }
    }
}
