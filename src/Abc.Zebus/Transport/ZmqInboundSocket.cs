using System;
using System.Text;
using Abc.Zebus.Serialization.Protobuf;
using log4net;
using ZeroMQ;

namespace Abc.Zebus.Transport
{
    internal class ZmqInboundSocket : IDisposable
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(ZmqInboundSocket));
        private readonly ZContext _context;
        private readonly PeerId _peerId;
        private readonly ZmqEndPoint _originalEndpoint;
        private readonly ZmqSocketOptions _options;
        private readonly string _environment;
        private byte[] _readBuffer = new byte[0];
        private ZSocket _socket;
        private ZmqEndPoint _socketEndPoint;
        private TimeSpan _lastReceiveTimeout;

        public ZmqInboundSocket(ZContext context, PeerId peerId, ZmqEndPoint originalEndpoint, ZmqSocketOptions options, string environment)
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

            var endPoint = new ZmqEndPoint(_originalEndpoint.Value); 
            if (endPoint.HasRandomPort)
                endPoint.SelectRandomPort(_environment);

            _socket.Bind(endPoint.Value);

            _socketEndPoint = new ZmqEndPoint(_socket.LastEndpoint);
            _logger.InfoFormat("Socket bound, Inbound EndPoint: {0}", _socketEndPoint.Value);

            return _socketEndPoint;
        }

        public void Dispose()
        {
            _socket.Dispose();
        }

        public CodedInputStream Receive(TimeSpan? timeout = null)
        {
            var receiveTimeout = timeout ?? _options.ReadTimeout;
            if (receiveTimeout != _lastReceiveTimeout)
            {
                _socket.ReceiveTimeout = receiveTimeout;
                _lastReceiveTimeout = receiveTimeout;
            }

            var frame = _socket.ReceiveFrame(out var error);
            if (error == null)
                return GetStream(frame);

            // EAGAIN: Non-blocking mode was requested and no messages are available at the moment.

            if (error.Number == ZError.EAGAIN)
                return null;

            throw new ZException(error);
        }

        private CodedInputStream GetStream(ZFrame frame)
        {
            var size = (int)frame.Length;
            if (size <= 0)
                return null;

            if (_readBuffer.Length < size)
                _readBuffer = new byte[size];

            frame.Read(_readBuffer, 0, size);

            return new CodedInputStream(_readBuffer, 0, size);
        }

        private ZSocket CreateSocket()
        {
            var socket = new ZSocket(_context, ZSocketType.PULL)
            {
                ReceiveHighWatermark = _options.ReceiveHighWaterMark,
                ReceiveTimeout = _options.ReadTimeout,
                Identity = Encoding.ASCII.GetBytes(_peerId.ToString()),
            };

            _lastReceiveTimeout = _options.ReadTimeout;

            return socket;
        }

        public void Disconnect()
        {
            var endpoint = _socket.LastEndpoint;
            if (endpoint == null)
                return;

            _logger.InfoFormat("Unbinding socket, Inbound Endpoint: {0}", endpoint);
            if (!_socket.Unbind(endpoint, out var error))
                _logger.WarnFormat("Socket error, Inbound Endpoint: {0}, Erro: {1}", endpoint, error);
        }
    }
}
