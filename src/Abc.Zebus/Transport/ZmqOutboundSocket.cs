using System;
using System.Diagnostics;
using Abc.Zebus.Transport.Zmq;
using Microsoft.Extensions.Logging;

namespace Abc.Zebus.Transport;

internal class ZmqOutboundSocket
{
    private static readonly ILogger _logger = ZebusLogManager.GetLogger(typeof(ZmqOutboundSocket));

    private readonly Stopwatch _closedStateStopwatch = new();
    private readonly ZmqContext _context;
    private readonly ZmqSocketOptions _options;
    private readonly IZmqOutboundSocketErrorHandler _errorHandler;
    private ZmqSocket? _socket;
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
            _socket = CreateSocket();
            _socket.Connect(EndPoint);

            IsConnected = true;

            _logger.LogInformation($"Socket connected, Peer: {PeerId}, EndPoint: {EndPoint}");
        }
        catch (Exception ex)
        {
            _socket?.Dispose();
            _socket = null;
            IsConnected = false;

            _logger.LogError(ex, $"Unable to connect socket, Peer: {PeerId}, EndPoint: {EndPoint}");
            _errorHandler.OnConnectException(PeerId, EndPoint, ex);

            SwitchToClosedState(_options.ClosedStateDurationAfterConnectFailure);
        }
    }

    private ZmqSocket CreateSocket()
    {
        var socket = new ZmqSocket(_context, ZmqSocketType.PUSH);

        socket.SetOption(ZmqSocketOption.SNDHWM, _options.SendHighWaterMark);
        socket.SetOption(ZmqSocketOption.SNDTIMEO, (int)_options.SendTimeout.TotalMilliseconds);

        if (_options.KeepAlive != null)
        {
            socket.SetOption(ZmqSocketOption.TCP_KEEPALIVE, _options.KeepAlive.Enabled ? 1 : 0);

            if (_options.KeepAlive.KeepAliveTimeout != null)
                socket.SetOption(ZmqSocketOption.TCP_KEEPALIVE_IDLE, (int)_options.KeepAlive.KeepAliveTimeout.Value.TotalSeconds);

            if (_options.KeepAlive.KeepAliveInterval != null)
                socket.SetOption(ZmqSocketOption.TCP_KEEPALIVE_INTVL, (int)_options.KeepAlive.KeepAliveInterval.Value.TotalSeconds);
        }

        return socket;
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
            _socket!.SetOption(ZmqSocketOption.LINGER, 0);
            _socket!.Dispose();

            _logger.LogInformation($"Socket disconnected, Peer: {PeerId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Unable to disconnect socket, Peer: {PeerId}");
            _errorHandler.OnDisconnectException(PeerId, EndPoint, ex);
        }

        IsConnected = false;
    }

    public void Send(byte[] buffer, int length, TransportMessage message)
    {
        if (!CanSendOrConnect(message))
            return;

        if (_socket!.TrySend(buffer, 0, length, out var error))
        {
            _failedSendCount = 0;
            return;
        }

        var hasReachedHighWaterMark = error == ZmqErrorCode.EAGAIN;
        var errorMessage = hasReachedHighWaterMark ? "High water mark reached" : error.ToErrorMessage();

        _logger.LogError($"Unable to send message, destination peer: {PeerId}, MessageTypeId: {message.MessageTypeId}, MessageId: {message.Id}, Error: {errorMessage}");
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
                _logger.LogWarning($"Send or connect ignored in closed state, Peer: {PeerId}, MessageTypeId: {message.MessageTypeId}, MessageId: {message.Id}");
                return false;
            }

            SwitchToOpenState();
        }

        return true;
    }

    private void SwitchToClosedState(TimeSpan duration)
    {
        _logger.LogError($"Switching to closed state, Peer: {PeerId}, Duration: {duration}");

        _closedStateStopwatch.Start();
        _closedStateDuration = duration;
        _isInClosedState = true;
    }

    private void SwitchToOpenState()
    {
        _logger.LogInformation($"Switching back to open state, Peer: {PeerId}");

        _isInClosedState = false;
        _closedStateStopwatch.Reset();
    }
}
