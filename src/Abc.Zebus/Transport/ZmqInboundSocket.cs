using System;
using Abc.Zebus.Util;
using log4net;
using ZeroMQ;

namespace Abc.Zebus.Transport
{
    internal class ZmqInboundSocket : IDisposable
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(ZmqInboundSocket));
        private readonly ZmqContext _context;
        private readonly PeerId _peerId;
        private readonly ZmqEndPoint _originalEndpoint;
        private readonly IZmqSocketOptions _options;
        private readonly string _environment;
        private byte[] _readBuffer = new byte[0];
        private ZmqSocket _socket;
        private ZmqEndPoint _endPoint;

        public ZmqInboundSocket(ZmqContext context, PeerId peerId, ZmqEndPoint originalEndpoint, IZmqSocketOptions options, string environment)
        {
            _context = context;
            _peerId = peerId;
            _originalEndpoint = originalEndpoint;
            _options = options;
            _environment = environment;
        }

        public ZmqEndPoint Bind()
        {
            _socket = CreateSocket();

            _endPoint = new ZmqEndPoint(_originalEndpoint.Value); 
            if (_endPoint.HasRandomPort)
                _endPoint.SelectRandomPort(_peerId, _environment);

            _socket.Bind(_endPoint.Value);

            var endPointWithIp = new ZmqEndPoint(_socket.LastEndpoint);
            _logger.InfoFormat("Socket bound, Inbound EndPoint: {0}", endPointWithIp.Value);

            return endPointWithIp;
        }

        public void Dispose()
        {
            _socket.Dispose();
        }

        public bool TryReceive(MutableMemoryStream inputBuffer, TimeSpan? timeout = null)
        {
            int size;

            _socket.ReceiveTimeout = timeout.HasValue ? timeout.Value : _options.ReadTimeout;
            _readBuffer = _socket.Receive(_readBuffer, TimeSpan.MaxValue, out size);

            if (size <= 0)
                return false;

            inputBuffer.SetBuffer(_readBuffer, 0, size);

            return true;
        }

        private ZmqSocket CreateSocket()
        {
            var socket = _context.CreateSocket(SocketType.PULL);
            socket.ReceiveHighWatermark = _options.ReceiveHighWaterMark;
            socket.SetPeerId(_peerId);

            return socket;
        }

        public void Disconnect()
        {
            if (_endPoint == null)
                return;

            _logger.InfoFormat("Unbinding socket, Inbound Endpoint: {0}", _endPoint.Value);
            _socket.Disconnect(_endPoint.Value);
        }
    }
}