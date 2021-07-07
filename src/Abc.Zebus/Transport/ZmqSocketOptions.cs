using System;
using Abc.Zebus.Util;

namespace Abc.Zebus.Transport
{
    /// <summary>
    /// Exposes options for Zebus underlying ZMQ context and sockets.
    /// </summary>
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

            MaximumSocketCount = 2048;

            KeepAlive = KeepAliveOptions.On(30.Seconds(), 3.Seconds());
        }

        [Obsolete("Use ReceiveTimeout instead")]
        public TimeSpan ReadTimeout
        {
            get => ReceiveTimeout;
            set => ReceiveTimeout = value;
        }

        /// <summary>
        /// Configures ZMQ_SNDHWM (high water mark for outbound messages).
        /// </summary>
        public int SendHighWaterMark { get; set; }

        /// <summary>
        /// Configures ZMQ_SNDTIMEO (maximum time before a send operation returns).
        /// </summary>
        public TimeSpan SendTimeout { get; set; }

        /// <summary>
        /// Number of send retries before a ZMQ socket is switched to the closed state.
        /// When a ZMQ socket is in the closed state, sent messages are dropped.
        ///
        /// <see cref="ZmqOutboundSocket.Send"/>.
        /// </summary>
        public int SendRetriesBeforeSwitchingToClosedState { get; set; }

        /// <summary>
        /// Duration of the socket closed state after send errors.
        /// </summary>
        public TimeSpan ClosedStateDurationAfterSendFailure { get; set; }

        /// <summary>
        /// Duration of the socket closed state after connection errors.
        /// </summary>
        public TimeSpan ClosedStateDurationAfterConnectFailure { get; set; }

        /// <summary>
        /// Configures ZMQ_RCVHWM (high water mark for inbound messages).
        /// </summary>
        public int ReceiveHighWaterMark { get; set; }

        /// <summary>
        /// Configures ZMQ_RCVTIMEO (maximum time before a receive operation returns).
        /// </summary>
        public TimeSpan ReceiveTimeout { get; set; }

        /// <summary>
        /// When specified, configures ZMQ_MAX_SOCKETS (maximum number of ZMQ sockets).
        /// When null, the default value from libzmq will be used (probably 1024).
        /// </summary>
        /// <remarks>
        /// One ZMQ socket can generate multiple TCP sockets. This is not the maximum number of TCP sockets.
        /// </remarks>
        public int? MaximumSocketCount { get; set; }

        /// <summary>
        /// Configures ZMQ keepalive options (ZMQ_TCP_KEEPALIVE, ZMQ_TCP_KEEPALIVE_IDLE and ZMQ_TCP_KEEPALIVE_INTVL).
        /// </summary>
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
