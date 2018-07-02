using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using log4net;
using ZeroMQ;

namespace Abc.Zebus.Transport
{
    public class ZmqOutboundSocket
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(ZmqOutboundSocket));
        private readonly Stopwatch _closedStateStopwatch = new Stopwatch();
        private readonly ZContext _context;
        private readonly ZmqSocketOptions _options;
        private readonly IZmqOutboundSocketErrorHandler _errorHandler;
        private ZSocket _socket;
        private int _failedSendCount;
        private bool _isInClosedState;
        private TimeSpan _closedStateDuration;

        public ZmqOutboundSocket(ZContext context, PeerId peerId, string endPoint, ZmqSocketOptions options, IZmqOutboundSocketErrorHandler errorHandler)
        {
            _context = context;
            _options = options;
            _errorHandler = errorHandler;
            PeerId = peerId;
            EndPoint = endPoint;
        }

        public PeerId PeerId { get; }
        public bool IsConnected { get; private set; }
        public string EndPoint { get; private set; }

        public void ConnectFor(TransportMessage message)
        {
            if (!CanSendOrConnect(message))
                return;

            try
            {
                _socket = new ZSocket(_context, ZSocketType.PUSH)
                {
                    SendHighWatermark = _options.SendHighWaterMark,
                    SendTimeout = _options.SendTimeout,
                    TcpKeepAlive = TcpKeepaliveBehaviour.Enable,
                    TcpKeepAliveIdle = 30,
                    TcpKeepAliveInterval = 3,
                    Identity = Encoding.ASCII.GetBytes(PeerId.ToString()),
                };

                _socket.Connect(EndPoint);

                IsConnected = true;

                _logger.InfoFormat("Socket connected, Peer: {0}, EndPoint: {1}", PeerId, EndPoint);
            }
            catch (Exception ex)
            {
                _socket.Dispose();
                _socket = null;
                IsConnected = false;

                _logger.ErrorFormat("Unable to connect socket, Peer: {0}, EndPoint: {1}, Exception: {2}", PeerId, EndPoint, ex);
                _errorHandler.OnConnectException(PeerId, EndPoint, ex);

                SwitchToClosedState(_options.ClosedStateDurationAfterConnectFailure);
            }
        }

        public void ReconnectFor(string endPoint, TransportMessage message)
        {
            Disconnect();
            EndPoint = endPoint;
            ConnectFor(message);
        }

        public void Disconnect()
        {
            if (!IsConnected)
                return;

            try
            {
                _socket.Linger = TimeSpan.Zero;
                _socket.Dispose();

                _logger.InfoFormat("Socket disconnected, Peer: {0}", PeerId);
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat("Unable to disconnect socket, Peer: {0}, Exception: {1}", PeerId, ex);
                _errorHandler.OnDisconnectException(PeerId, EndPoint, ex);
            }

            IsConnected = false;
        }

        public void Send(byte[] buffer, int length, TransportMessage message)
        {
            if (!CanSendOrConnect(message))
                return;

            var stopwatch = Stopwatch.StartNew();
            var spinWait = new SpinWait();

            ZError error;

            while (true)
            {
                if (_socket.SendBytes(buffer, 0, length, ZSocketFlags.None, out error))
                {
                _failedSendCount = 0;
                return;
            }

                // EAGAIN: Non-blocking mode was requested and the message cannot be sent at the moment.

                var shouldRetry = error.Number == ZError.EAGAIN;
                if (shouldRetry && stopwatch.Elapsed < _options.SendTimeout)
                {
                    spinWait.SpinOnce();
                    continue;
                }

                break;
            }

            _logger.ErrorFormat("Unable to send message, destination peer: {0}, MessageTypeId: {1}, MessageId: {2}, Error: {3}", PeerId, message.MessageTypeId, message.Id, error);
            _errorHandler.OnSendFailed(PeerId, EndPoint, message.MessageTypeId, message.Id);

            if (_failedSendCount >= _options.SendRetriesBeforeSwitchingToClosedState)
                SwitchToClosedState(_options.ClosedStateDurationAfterSendFailure);

            ++_failedSendCount;
        }

        private bool CanSendOrConnect(TransportMessage message)
        {
            if (_isInClosedState)
            {
                if (_closedStateStopwatch.Elapsed < _closedStateDuration)
                {
                    _logger.WarnFormat("Send or connect ignored in closed state, Peer: {0}, MessageTypeId: {1}, MessageId: {2}", PeerId, message.MessageTypeId, message.Id);
                    return false;
                }

                SwitchToOpenState();
            }

            return true;
        }

        private void SwitchToClosedState(TimeSpan duration)
        {
            _logger.ErrorFormat("Switching to closed state, Peer: {0}, Duration: {1}", PeerId, duration);

            _closedStateStopwatch.Start();
            _closedStateDuration = duration;
            _isInClosedState = true;
        }

        private void SwitchToOpenState()
        {
            _logger.InfoFormat("Switching back to open state, Peer: {0}", PeerId);

            _isInClosedState = false;
            _closedStateStopwatch.Reset();
        }
    }
}
