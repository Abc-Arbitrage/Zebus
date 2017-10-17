using System;
using System.Diagnostics;
using System.IO;
using log4net;
using ZeroMQ;

namespace Abc.Zebus.Transport
{
    public class ZmqOutboundSocket
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(ZmqOutboundSocket));
        private readonly Stopwatch _closedStateStopwatch = new Stopwatch();
        private readonly ZmqContext _context;
        private readonly ZmqSocketOptions _options;
        private readonly IZmqOutboundSocketErrorHandler _errorHandler;
        private ZmqSocket _socket;
        private int _failedSendCount;
        private bool _isInClosedState;
        private TimeSpan _closedStateDuration;

        public ZmqOutboundSocket(ZmqContext context, PeerId peerId, string endPoint, ZmqSocketOptions options, IZmqOutboundSocketErrorHandler errorHandler)
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
                _socket = _context.CreateSocket(SocketType.PUSH);
                _socket.SendHighWatermark = _options.SendHighWaterMark;
                _socket.TcpKeepalive = TcpKeepaliveBehaviour.Enable;
                _socket.TcpKeepaliveIdle = 30;
                _socket.TcpKeepaliveIntvl = 3;
                _socket.SetPeerId(PeerId);

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

            if (_socket.SendWithTimeout(buffer, length, _options.SendTimeout) >= 0)
            {
                _failedSendCount = 0;
                return;
            }

            _logger.ErrorFormat("Unable to send message, destination peer: {0}, MessageTypeId: {1}, MessageId: {2}", PeerId, message.MessageTypeId, message.Id);
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
                    _logger.DebugFormat("Send or connect ignored in closed state, Peer: {0}, MessageTypeId: {1}, MessageId: {2}", PeerId, message.MessageTypeId, message.Id);
                    return false;
                }
                SwitchToOpenState();
            }
            return true;
        }

        private void SwitchToClosedState(TimeSpan duration)
        {
            _logger.InfoFormat("Switching to closed state, Peer: {0}, Duration: {1}", PeerId, duration);

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