using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Abc.Zebus.Directory;
using Abc.Zebus.Serialization.Protobuf;
using Abc.Zebus.Transport.Zmq;
using Abc.Zebus.Util;
using Microsoft.Extensions.Logging;

namespace Abc.Zebus.Transport;

public class ZmqTransport : ITransport
{
    private readonly IZmqTransportConfiguration _configuration;
    private readonly ZmqSocketOptions _socketOptions;
    private readonly IZmqOutboundSocketErrorHandler _errorHandler;
    private readonly ZmqEndPoint _configuredInboundEndPoint;
    private ILogger _logger = ZebusLogManager.GetLogger(typeof(ZmqTransport));
    private ConcurrentDictionary<PeerId, ZmqOutboundSocket> _outboundSockets = new();
    private BlockingCollection<OutboundSocketAction>? _outboundSocketActions;
    private BlockingCollection<PendingDisconnect> _pendingDisconnects = new();
    private ZmqContext? _context;
    private Thread? _inboundThread;
    private Thread? _outboundThread;
    private Thread? _disconnectThread;
    private volatile bool _isListening;
    private ZmqEndPoint? _effectiveInboundEndPoint;
    private string _environment = string.Empty;
    private CountdownEvent? _outboundSocketsToStop;
    private bool _isRunning;

    public ZmqTransport(IZmqTransportConfiguration configuration, ZmqSocketOptions socketOptions, IZmqOutboundSocketErrorHandler errorHandler)
    {
        _configuration = configuration;
        _socketOptions = socketOptions;
        _errorHandler = errorHandler;
        _configuredInboundEndPoint = new ZmqEndPoint(configuration.InboundEndPoint);
    }

    public event Action<TransportMessage>? MessageReceived;

    public bool IsListening => _isListening;

    /// <remarks>
    /// The configured endpoint has probably a random port (e.g.: tcp://192.168.0.1:* or tcp://*:*).
    /// The effective inbound endpoint can only known after <see cref="ZmqInboundSocket.Bind"/>.
    /// </remarks>
    public string InboundEndPoint => (_effectiveInboundEndPoint ?? _configuredInboundEndPoint).ToString();

    public int PendingSendCount => _outboundSocketActions?.Count ?? 0;

    public int OutboundSocketCount => _outboundSockets.Count;

    public PeerId PeerId { get; private set; }

    internal void SetLogId(int logId)
    {
        _logger = ZebusLogManager.GetLogger(typeof(ZmqTransport).FullName + "#" + logId);
    }

    public void Configure(PeerId peerId, string environment)
    {
        PeerId = peerId;
        _environment = environment;
    }

    public void OnPeerUpdated(PeerId peerId, PeerUpdateAction peerUpdateAction)
    {
        if (peerId == PeerId)
            return;

        if (peerUpdateAction == PeerUpdateAction.Decommissioned && !peerId.IsPersistence())
            Disconnect(peerId);

        // Forgetting a peer when it starts up make sure we don't have a stale socket for it, at the cost of losing the send buffer safety
        if (peerUpdateAction == PeerUpdateAction.Started)
            Disconnect(peerId, delayed: false);
    }

    public void OnRegistered()
    {
    }

    public void Start()
    {
        var zmqVersion = ZmqUtil.GetVersion();
        _logger.LogInformation($"Loaded ZMQ v{zmqVersion.ToString(3)}");

        if (zmqVersion.Major != 4)
            throw new InvalidOperationException($"Expected ZMQ v4.*, loaded ZMQ v{zmqVersion.ToString(3)}");

        _isListening = true;

        _outboundSockets = new ConcurrentDictionary<PeerId, ZmqOutboundSocket>();
        _outboundSocketActions = new BlockingCollection<OutboundSocketAction>();
        _pendingDisconnects = new BlockingCollection<PendingDisconnect>();
        _context = new ZmqContext();

        if (_socketOptions.MaximumSocketCount != null)
            _context.SetOption(ZmqContextOption.MAX_SOCKETS, _socketOptions.MaximumSocketCount.Value);

        var startSequenceState = new InboundProcStartSequenceState();

        _inboundThread = BackgroundThread.Start(InboundProc, startSequenceState);
        _outboundThread = BackgroundThread.Start(OutboundProc);
        _disconnectThread = BackgroundThread.Start(DisconnectProc);

        startSequenceState.Wait();
        _isRunning = true;
    }

    public void Stop()
    {
        Stop(false);
    }

    public void Stop(bool discardPendingMessages)
    {
        if (!_isRunning)
            return;

        _pendingDisconnects.CompleteAdding();

        if (discardPendingMessages)
            DiscardItems(_pendingDisconnects);

        if (!_disconnectThread!.Join(30.Seconds()))
            _logger.LogError("Unable to terminate disconnect thread");

        _outboundSocketActions!.CompleteAdding();

        if (discardPendingMessages)
            DiscardItems(_outboundSocketActions!);

        if (!_outboundThread!.Join(30.Seconds()))
            _logger.LogError("Unable to terminate outbound thread");

        _isListening = false;
        if (!_inboundThread!.Join(30.Seconds()))
            _logger.LogError("Unable to terminate inbound thread");

        _outboundSocketActions!.Dispose();
        _outboundSocketActions = null;

        _context!.Dispose();
        _logger.LogInformation($"{PeerId} Stopped");
    }

    private static void DiscardItems<T>(BlockingCollection<T> collection)
    {
        while (collection.TryTake(out _))
        {
        }
    }

    public void Send(TransportMessage message, IEnumerable<Peer> peers)
    {
        Send(message, peers, new SendContext());
    }

    public void Send(TransportMessage message, IEnumerable<Peer> peers, SendContext context)
    {
        _outboundSocketActions!.Add(OutboundSocketAction.Send(message, peers, context));
    }

    private void Disconnect(PeerId peerId, bool delayed = true)
    {
        if (_outboundSockets.ContainsKey(peerId))
            _logger.LogInformation($"Queueing disconnect, PeerId: {peerId}, Delayed: {delayed}");

        if (delayed)
        {
            SafeAdd(_pendingDisconnects, new PendingDisconnect(peerId, SystemDateTime.UtcNow.Add(_configuration.WaitForEndOfStreamAckTimeout)));
        }
        else
        {
            SafeAdd(_outboundSocketActions!, OutboundSocketAction.Disconnect(peerId));
        }
    }

    public void AckMessage(TransportMessage transportMessage)
    {
    }

    private void InboundProc(InboundProcStartSequenceState state)
    {
        Thread.CurrentThread.Name = "ZmqTransport.InboundProc";
        _logger.LogDebug("Starting inbound proc...");

        var inboundSocket = CreateInboundSocket(state);
        if (inboundSocket == null)
            return;

        using (inboundSocket)
        {
            while (_isListening)
            {
                var bufferReader = inboundSocket.Receive();
                if (bufferReader == null)
                    continue;

                DeserializeAndForwardTransportMessage(bufferReader);
            }

            GracefullyDisconnectInboundSocket(inboundSocket);
        }

        _logger.LogInformation("InboundProc terminated");
    }

    private ZmqInboundSocket? CreateInboundSocket(InboundProcStartSequenceState state)
    {
        ZmqInboundSocket? inboundSocket = null;
        try
        {
            inboundSocket = new ZmqInboundSocket(_context!, _configuredInboundEndPoint, _socketOptions);
            _effectiveInboundEndPoint = inboundSocket.Bind();
            return inboundSocket;
        }
        catch (Exception ex)
        {
            state.SetFailed(ex);
            inboundSocket?.Dispose();

            return null;
        }
        finally
        {
            state.Release();
        }
    }

    private void GracefullyDisconnectInboundSocket(ZmqInboundSocket inboundSocket)
    {
        inboundSocket.Disconnect();

        ProtoBufferReader? bufferReader;
        while ((bufferReader = inboundSocket.Receive(100.Milliseconds())) != null)
            DeserializeAndForwardTransportMessage(bufferReader);
    }

    private void DeserializeAndForwardTransportMessage(ProtoBufferReader bufferReader)
    {
        try
        {
            if (!TryDeserializeTransportMessage(bufferReader, out var transportMessage))
                return;

            if (!ValidateTransportMessage(transportMessage))
                return;

            if (transportMessage.MessageTypeId == MessageTypeId.EndOfStream)
            {
                SendEndOfStreamAck(transportMessage);
                return;
            }

            if (transportMessage.MessageTypeId == MessageTypeId.EndOfStreamAck)
            {
                OnEndOfStreamAck(transportMessage);
                return;
            }

            if (_isListening)
                MessageReceived?.Invoke(transportMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process inbound transport message");
        }
    }

    private bool TryDeserializeTransportMessage(ProtoBufferReader bufferReader, out TransportMessage transportMessage)
    {
        if (bufferReader.TryReadTransportMessage(out transportMessage))
            return true;

        _logger.LogDebug($"Unable to read transport message, Length: {bufferReader.Length}, Bytes: {bufferReader.ToDebugString(50)}");

        return false;
    }

    private bool ValidateTransportMessage(TransportMessage transportMessage)
    {
        var isValid = transportMessage.Id.Value != default
                      && transportMessage.MessageTypeId.FullName != default
                      && transportMessage.Originator.SenderId != default;

        if (!isValid)
        {
            _logger.LogDebug($"Invalid transport message received, Id: {transportMessage.Id}, MessageTypeId: {transportMessage.MessageTypeId}, Originator: {transportMessage.Originator.SenderId}");
            return false;
        }

        if (transportMessage.Environment == null) // TODO: treat as invalid message
        {
            _logger.LogInformation($"Receiving message with null environment from  {transportMessage.Originator.SenderId}");
        }
        else if (transportMessage.Environment != _environment)
        {
            _logger.LogError($"Receiving messages from wrong environment: {transportMessage.Environment} from {transportMessage.Originator.SenderEndPoint}, discarding message type {transportMessage.MessageTypeId}");
            return false;
        }

        return true;
    }

    private void OnEndOfStreamAck(TransportMessage transportMessage)
    {
        var senderId = transportMessage.Originator.SenderId;
        var senderEndPoint = transportMessage.Originator.SenderEndPoint;

        if (!_outboundSockets.ContainsKey(senderId))
        {
            _logger.LogError("Received EndOfStreamAck for an unknown socket ({0}) PeerId: {1} (Known peers: {2})", senderEndPoint, senderId, string.Join(", ", _outboundSockets.Keys));
            return;
        }

        _logger.LogInformation("Received EndOfStreamAck for {0}, {1}", senderId, senderEndPoint);

        _outboundSocketsToStop!.Signal();
    }

    private void SendEndOfStreamAck(TransportMessage transportMessage)
    {
        _logger.LogInformation("Sending EndOfStreamAck to {0}", transportMessage.Originator.SenderEndPoint);

        var endOfStreamAck = new TransportMessage(MessageTypeId.EndOfStreamAck, default, PeerId, InboundEndPoint);
        var closingPeer = new Peer(transportMessage.Originator.SenderId, transportMessage.Originator.SenderEndPoint);

        SafeAdd(_outboundSocketActions!, OutboundSocketAction.Send(endOfStreamAck, new[] { closingPeer }, new SendContext()));
        SafeAdd(_pendingDisconnects!, new PendingDisconnect(closingPeer.Id, SystemDateTime.UtcNow.Add(_configuration.WaitForEndOfStreamAckTimeout)));
    }

    private void OutboundProc()
    {
        Thread.CurrentThread.Name = "ZmqTransport.OutboundProc";
        _logger.LogDebug("Starting outbound proc...");

        var bufferWriter = new ProtoBufferWriter();

        foreach (var socketAction in _outboundSocketActions!.GetConsumingEnumerable())
        {
            if (socketAction.IsDisconnect)
            {
                DisconnectPeers(socketAction.Targets.Select(x => x.Id));
            }
            else
            {
                WriteTransportMessageAndSendToPeers(socketAction.Message, socketAction.Targets, socketAction.Context, bufferWriter);
            }
        }

        GracefullyDisconnectOutboundSockets(bufferWriter);

        _logger.LogInformation("OutboundProc terminated");
    }

    private void WriteTransportMessageAndSendToPeers(TransportMessage transportMessage, List<Peer> peers, SendContext context, ProtoBufferWriter bufferWriter)
    {
        bufferWriter.Reset();
        bufferWriter.WriteTransportMessage(transportMessage, _environment);

        if (context.PersistencePeer == null && transportMessage.IsPersistTransportMessage)
        {
            bufferWriter.WritePersistentPeerIds(transportMessage, transportMessage.PersistentPeerIds);
        }

        foreach (var target in peers)
        {
            var isPersistent = context.WasPersisted(target.Id);
            bufferWriter.SetWasPersisted(isPersistent);

            SendToPeer(transportMessage, bufferWriter, target);
        }

        if (context.PersistencePeer != null)
        {
            bufferWriter.WritePersistentPeerIds(transportMessage, context.PersistentPeerIds);

            SendToPeer(transportMessage, bufferWriter, context.PersistencePeer);
        }
    }

    private void SendToPeer(TransportMessage transportMessage, ProtoBufferWriter bufferWriter, Peer target)
    {
        var outboundSocket = GetConnectedOutboundSocket(target, transportMessage);
        if (!outboundSocket.IsConnected)
        {
            _logger.LogError($"Could not send message of type {transportMessage.MessageTypeId.FullName} to peer {target.Id} because outbound socket was not connected");
            return;
        }

        try
        {
            outboundSocket.Send(bufferWriter.Buffer, bufferWriter.Position, transportMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send message, PeerId: {target.Id}, EndPoint: {target.EndPoint}");
        }
    }

    private void DisconnectPeers(IEnumerable<PeerId> peerIds)
    {
        foreach (var peerId in peerIds)
        {
            if (!_outboundSockets.TryRemove(peerId, out var outboundSocket))
                continue;

            outboundSocket.Disconnect();
        }
    }

    private ZmqOutboundSocket GetConnectedOutboundSocket(Peer peer, TransportMessage transportMessage)
    {
        if (!_outboundSockets.TryGetValue(peer.Id, out var outboundSocket))
        {
            outboundSocket = new ZmqOutboundSocket(_context!, peer.Id, peer.EndPoint, _socketOptions, _errorHandler);
            outboundSocket.ConnectFor(transportMessage);

            _outboundSockets.TryAdd(peer.Id, outboundSocket);
        }
        else if (!string.Equals(outboundSocket.EndPoint, peer.EndPoint, StringComparison.OrdinalIgnoreCase))
        {
            outboundSocket.ReconnectFor(peer.EndPoint, transportMessage);
        }

        return outboundSocket;
    }

    private void GracefullyDisconnectOutboundSockets(ProtoBufferWriter bufferWriter)
    {
        var connectedOutboundSockets = _outboundSockets.Values.Where(x => x.IsConnected).ToList();

        _outboundSocketsToStop = new CountdownEvent(connectedOutboundSockets.Count);

        SendEndOfStreamMessages(connectedOutboundSockets, bufferWriter);

        _logger.LogInformation($"Waiting for {_outboundSocketsToStop.InitialCount} outbound socket end of stream acks");
        if (!_outboundSocketsToStop.Wait(_configuration.WaitForEndOfStreamAckTimeout))
            _logger.LogWarning($"{_outboundSocketsToStop.CurrentCount} peers did not respond to end of stream");

        DisconnectPeers(connectedOutboundSockets.Select(x => x.PeerId).ToList());
    }

    private void SendEndOfStreamMessages(List<ZmqOutboundSocket> connectedOutboundSockets, ProtoBufferWriter bufferWriter)
    {
        foreach (var outboundSocket in connectedOutboundSockets)
        {
            _logger.LogInformation($"Sending EndOfStream to {outboundSocket.EndPoint}");

            var endOfStreamMessage = new TransportMessage(MessageTypeId.EndOfStream, default, PeerId, InboundEndPoint) { WasPersisted = false };
            bufferWriter.Reset();
            bufferWriter.WriteTransportMessage(endOfStreamMessage, _environment);
            outboundSocket.Send(bufferWriter.Buffer, bufferWriter.Position, endOfStreamMessage);
        }
    }

    private void DisconnectProc()
    {
        Thread.CurrentThread.Name = "ZmqTransport.DisconnectProc";

        foreach (var pendingDisconnect in _pendingDisconnects.GetConsumingEnumerable())
        {
            while (pendingDisconnect.DisconnectTimeUtc > SystemDateTime.UtcNow)
            {
                if (_pendingDisconnects.IsAddingCompleted)
                    return;

                Thread.Sleep(500);
            }

            SafeAdd(_outboundSocketActions!, OutboundSocketAction.Disconnect(pendingDisconnect.PeerId));
        }
    }

    private void SafeAdd<T>(BlockingCollection<T> collection, T item)
    {
        try
        {
            collection.Add(item);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"Unable to enqueue item, Type: {typeof(T).Name}");
        }
    }

    private readonly struct OutboundSocketAction
    {
        private static readonly TransportMessage _disconnectMessage = new(default, null!, new PeerId(), null!);

        private OutboundSocketAction(TransportMessage message, IEnumerable<Peer> targets, SendContext context)
        {
            Message = message;
            Targets = targets as List<Peer> ?? targets.ToList();
            Context = context;
        }

        public bool IsDisconnect => Message == _disconnectMessage;
        public TransportMessage Message { get; }
        public List<Peer> Targets { get; }
        public SendContext Context { get; }

        public static OutboundSocketAction Send(TransportMessage message, IEnumerable<Peer> peers, SendContext context)
            => new(message, peers, context);

        public static OutboundSocketAction Disconnect(PeerId peerId)
            => new(_disconnectMessage, new List<Peer> { new(peerId, null!) }, null!);
    }

    private class PendingDisconnect
    {
        public readonly PeerId PeerId;
        public readonly DateTime DisconnectTimeUtc;

        public PendingDisconnect(PeerId peerId, DateTime disconnectTimeUtc)
        {
            PeerId = peerId;
            DisconnectTimeUtc = disconnectTimeUtc;
        }
    }

    private class InboundProcStartSequenceState
    {
        private Exception? _inboundProcStartException;
        private readonly ManualResetEvent _inboundProcStartedSignal = new(false);

        public void Wait()
        {
            _inboundProcStartedSignal.WaitOne();
            if (_inboundProcStartException != null)
                throw _inboundProcStartException;
        }

        public void SetFailed(Exception exception)
        {
            _inboundProcStartException = exception;
        }

        public void Release()
        {
            _inboundProcStartedSignal.Set();
        }
    }
}
