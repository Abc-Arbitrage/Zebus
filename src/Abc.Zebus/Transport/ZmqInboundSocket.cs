using System;
using System.Text;
using Abc.Zebus.Serialization.Protobuf;
using Abc.Zebus.Transport.Zmq;
using log4net;

namespace Abc.Zebus.Transport
{
    internal class ZmqInboundSocket : IDisposable
    {
        private static readonly ILog _logger = LogManager.GetLogger(typeof(ZmqInboundSocket));

        private readonly ZmqContext _context;
        private readonly PeerId _peerId;
        private readonly ZmqEndPoint _configuredEndPoint;
        private readonly ZmqSocketOptions _options;
        private byte[] _readBuffer = new byte[0];
        private ZmqSocket? _socket;
        private TimeSpan _lastReceiveTimeout;

        public ZmqInboundSocket(ZmqContext context, PeerId peerId, ZmqEndPoint configuredEndPoint, ZmqSocketOptions options)
        {
            _context = context;
            _peerId = peerId;
            _configuredEndPoint = configuredEndPoint;
            _options = options;
        }

        public ZmqEndPoint Bind()
        {
            _socket = CreateSocket();

            _socket.Bind(_configuredEndPoint.ToString());

            var socketEndPoint = new ZmqEndPoint(_socket.GetOptionString(ZmqSocketOption.LAST_ENDPOINT));
            _logger.InfoFormat("Socket bound, Inbound EndPoint: {0}", socketEndPoint);

            return socketEndPoint;
        }

        public void Dispose()
        {
            _socket?.Dispose();
        }

        // TODO: return Span instead of ProtoBufferReader
        public ProtoBufferReader? Receive(TimeSpan? timeout = null)
        {
            var receiveTimeout = timeout ?? _options.ReceiveTimeout;
            if (receiveTimeout != _lastReceiveTimeout)
            {
                _socket!.SetOption(ZmqSocketOption.RCVTIMEO, (int)receiveTimeout.TotalMilliseconds);
                _lastReceiveTimeout = receiveTimeout;
            }

            if (_socket!.TryReadMessage(ref _readBuffer, out var messageLength, out var error))
                return new ProtoBufferReader(_readBuffer, messageLength);

            // EAGAIN: Non-blocking mode was requested and no messages are available at the moment.
            if (error == ZmqErrorCode.EAGAIN || messageLength == 0)
                return null;

            throw ZmqUtil.ThrowLastError("ZMQ Receive error");
        }

        private ZmqSocket CreateSocket()
        {
            var socket = new ZmqSocket(_context, ZmqSocketType.PULL);
            socket.SetOption(ZmqSocketOption.RCVHWM, _options.ReceiveHighWaterMark);
            socket.SetOption(ZmqSocketOption.RCVTIMEO, (int)_options.ReceiveTimeout.TotalMilliseconds);

            _lastReceiveTimeout = _options.ReceiveTimeout;

            return socket;
        }

        public void Disconnect()
        {
            var endpoint = _socket?.GetOptionString(ZmqSocketOption.LAST_ENDPOINT);
            if (endpoint == null)
                return;

            _logger.InfoFormat("Unbinding socket, Inbound Endpoint: {0}", endpoint);
            if (!_socket!.TryUnbind(endpoint))
                _logger.WarnFormat("Socket error, Inbound Endpoint: {0}, Error: {1}", endpoint, ZmqUtil.GetLastErrorMessage());
        }
    }
}
