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
        private readonly ZmqContext _context;
        private readonly PeerId _peerId;
        private readonly IZmqSocketOptions _options;
        private ZmqSocket _socket;
        private int _failedSendCount;
        private bool _isInClosedState;
        private Stopwatch _closedStateStopwatch;
        private bool _isConnected;

        public ZmqOutboundSocket(ZmqContext context, PeerId peerId, string endPoint, IZmqSocketOptions options)
        {
            _context = context;
            _peerId = peerId;
            _options = options;
            EndPoint = endPoint;
        }

        public string EndPoint { get; private set; }

        public void Connect()
        {
            _socket = _context.CreateSocket(SocketType.PUSH);
            _socket.SendHighWatermark = _options.SendHighWaterMark;
            _socket.TcpKeepalive = TcpKeepaliveBehaviour.Enable;
            _socket.TcpKeepaliveIdle = 30;
            _socket.TcpKeepaliveIntvl = 3;
            _socket.SetPeerId(_peerId);

            try
            {
                _socket.Connect(EndPoint);
            }
            catch
            {
                _socket.Dispose();
                throw;
            }

            _logger.InfoFormat("Socket connected, Peer: {0}, EndPoint: {1}", _peerId, EndPoint);
            _isConnected = true;
        }

        public void Reconnect(string endPoint)
        {
            Disconnect();
            EndPoint = endPoint;
            Connect();
        }

        public void Disconnect()
        {
            if (!_isConnected)
                return;

            _socket.Linger = TimeSpan.Zero;
            _socket.Dispose();

            _logger.InfoFormat("Socket disconnected, Peer: {0}", _peerId);
            _isConnected = false;
        }

        public void Send(MemoryStream buffer, TransportMessage message)
        {
            if (_isInClosedState)
            {
                if (_closedStateStopwatch.Elapsed < _options.ClosedStateDuration)
                {
                    _logger.DebugFormat("Send ignored in closed state, Peer: {0}, MessageTypeId: {1}, MessageId: {2}", _peerId, message.MessageTypeId, message.Id);
                    return;
                }
                SwitchToOpenState();
            }

            if (_socket.SendWithTimeout(buffer.GetBuffer(), (int)buffer.Position, _options.SendTimeout) >= 0)
            {
                _failedSendCount = 0;
                return;
            }

            _logger.ErrorFormat("Unable to send message, destination peer: {0}, MessageTypeId: {1}, MessageId: {2}", _peerId, message.MessageTypeId, message.Id);

            if (_failedSendCount >= _options.SendRetriesBeforeSwitchingToClosedState)
                SwitchToClosedState();

            ++_failedSendCount;
        }

        private void SwitchToClosedState()
        {
            _logger.InfoFormat("Switching to closed state, Peer: {0}", _peerId);

            _closedStateStopwatch = Stopwatch.StartNew();
            _isInClosedState = true;
        }

        private void SwitchToOpenState()
        {
            _logger.InfoFormat("Switching back to open state, Peer: {0}", _peerId);

            _isInClosedState = false;
            _closedStateStopwatch = null;
        }
    }
}