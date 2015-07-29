using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Abc.Zebus.Directory;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Extensions;
using log4net;
using ProtoBuf;
using ZeroMQ;

namespace Abc.Zebus.Transport
{
    public class ZmqTransport : ITransport
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(ZmqTransport));
        private readonly IZmqTransportConfiguration _configuration;
        private readonly IZmqSocketOptions _socketOptions;
        private readonly ZmqEndPoint _configuredInboundEndPoint;
        private ConcurrentDictionary<PeerId, ZmqOutboundSocket> _outboundSockets;
        private BlockingCollection<OutboundSocketAction> _outboundSocketActions;
        private BlockingCollection<PendingDisconnect> _pendingDisconnects;
        private ZmqContext _context;
        private Thread _inboundThread;
        private Thread _outboundThread;
        private Thread _disconnectThread;
        private volatile bool _isListening;
        private ZmqEndPoint _realInboundEndPoint;
        private string _environment;
        private CountdownEvent _outboundSocketsToStop;
        private bool _isRunning;

        static ZmqTransport()
        {
            ExtractLibZmq("x64");
            ExtractLibZmq("x86");
        }

        static void ExtractLibZmq(string platform)
        {
            var resourceName = string.Format("libzmq-{0}-0.0.0.0.dll", platform);

            var libraryPath = PathUtil.InBaseDirectory(resourceName);
            if (File.Exists(libraryPath))
                return;

            var transportType = typeof(ZmqTransport);
            using (var resourceStream = transportType.Assembly.GetManifestResourceStream(transportType, resourceName))
            {
                if (resourceStream == null)
                    throw new Exception("Unable to find libzmq in the embedded resources.");

                using (var libraryFileStream = new FileStream(libraryPath, FileMode.OpenOrCreate, FileAccess.Write))
                {
                    resourceStream.CopyTo(libraryFileStream);
                }
            }
        }
        
        public ZmqTransport(IZmqTransportConfiguration configuration, IZmqSocketOptions socketOptions)
        {
            _configuration = configuration;
            _configuredInboundEndPoint = new ZmqEndPoint(configuration.InboundEndPoint);
            _socketOptions = socketOptions;
        }

        public event Action<TransportMessage> MessageReceived = delegate { };

        public virtual bool IsListening
        {
            get { return _isListening; }
            internal set { _isListening = value; }
        }

        public string InboundEndPoint
        {
            get { return _realInboundEndPoint != null ? _realInboundEndPoint.Value : _configuredInboundEndPoint.Value; }
        }

        public int PendingSendCount
        {
            get { return _outboundSocketActions == null ? 0 : _outboundSocketActions.Count; }
        }

        public IZmqSocketOptions SocketOptions
        {
            get { return _socketOptions; }
        }

        public int OutboundSocketCount
        {
            get { return _outboundSockets.Count; }
        }

        public PeerId PeerId { get; private set; }

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
            IsListening = true;

            _outboundSockets = new ConcurrentDictionary<PeerId, ZmqOutboundSocket>();
            _outboundSocketActions = new BlockingCollection<OutboundSocketAction>();
            _pendingDisconnects = new BlockingCollection<PendingDisconnect>();
            _context = ZmqContext.Create();

            var startSequenceState = new InboundProcStartSequenceState();

            _inboundThread = BackgroundThread.Start(InboundProc, startSequenceState, null);
            _outboundThread = BackgroundThread.Start(OutboundProc);
            _disconnectThread = BackgroundThread.Start(DisconnectProc);

            startSequenceState.Wait();
            _isRunning = true;
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            _pendingDisconnects.CompleteAdding();
            if (!_disconnectThread.Join(30.Seconds()))
                _logger.Error("Unable to terminate disconnect thread");

            _outboundSocketActions.CompleteAdding();
            if (!_outboundThread.Join(30.Seconds()))
                _logger.Error("Unable to terminate outbound thread");

            IsListening = false;
            if (!_inboundThread.Join(30.Seconds()))
                _logger.Error("Unable to terminate inbound thread");

            _outboundSocketActions.Dispose();
            _outboundSocketActions = null;

            _context.Dispose();
            _logger.InfoFormat("{0} Stopped", PeerId);
        }

        public void Send(TransportMessage message, IEnumerable<Peer> peerIds)
        {
            message.Environment = _environment;

            _outboundSocketActions.Add(OutboundSocketAction.Send(message, peerIds.ToList()));
        }

        private void Disconnect(PeerId peerId, bool delayed = true)
        {
            if (_outboundSockets.ContainsKey(peerId))
                _logger.InfoFormat("Queueing disconnect, PeerId: {0}, Delayed: {1}", peerId, delayed);

            if (delayed)
            {
                SafeAdd(_pendingDisconnects, new PendingDisconnect(peerId, SystemDateTime.UtcNow.Add(_configuration.WaitForEndOfStreamAckTimeout)));
            }
            else
            {
                SafeAdd(_outboundSocketActions, OutboundSocketAction.Disconnect(peerId));
            }
        }

        public void AckMessage(TransportMessage transportMessage)
        {
        }

        private void InboundProc(InboundProcStartSequenceState state)
        {
            Thread.CurrentThread.Name = "ZmqTransport.InboundProc";
            _logger.DebugFormat("Starting inbound proc...");

            var inboundSocket = CreateInboundSocket(state);
            if (inboundSocket == null)
                return;

            using (inboundSocket)
            {
                var inputBuffer = new MutableMemoryStream();
                while (IsListening)
                {
                    if (!inboundSocket.TryReceive(inputBuffer))
                        continue;

                    DeserializeAndForwardTransportMessage(inputBuffer);
                }

                GracefullyDisconnectInboundSocket(inboundSocket, inputBuffer);
            }

            _logger.InfoFormat("InboundProc terminated");
        }

        private ZmqInboundSocket CreateInboundSocket(InboundProcStartSequenceState state)
        {
            ZmqInboundSocket inboundSocket = null;
            try
            {
                inboundSocket = new ZmqInboundSocket(_context, PeerId, _configuredInboundEndPoint, _socketOptions, _environment);
                _realInboundEndPoint = inboundSocket.Bind();
                return inboundSocket;
            }
            catch (Exception ex)
            {
                state.SetFailed(ex);
                if (inboundSocket != null)
                    inboundSocket.Dispose();

                return null;
            }
            finally
            {
                state.Release();
            }
        }

        private void GracefullyDisconnectInboundSocket(ZmqInboundSocket inboundSocket, MutableMemoryStream inputBuffer)
        {
            inboundSocket.Disconnect();

            while (inboundSocket.TryReceive(inputBuffer, 100.Milliseconds()))
                DeserializeAndForwardTransportMessage(inputBuffer);
        }

        private void DeserializeAndForwardTransportMessage(Stream inputBuffer)
        {
            try
            {
                var transportMessage = Serializer.Deserialize<TransportMessage>(inputBuffer);
                if (!IsFromCurrentEnvironment(transportMessage))
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

                if (IsListening)
                    MessageReceived(transportMessage);
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat("Failed to process inbound transport message: {0}", ex);
            }
        }

        private void OnEndOfStreamAck(TransportMessage transportMessage)
        {
            var senderId = transportMessage.Originator.SenderId;
            var senderEndPoint = transportMessage.Originator.SenderEndPoint;

            if (!_outboundSockets.ContainsKey(senderId))
            {
                _logger.ErrorFormat("Received EndOfStreamAck for an unknown socket ({0}) PeerId: {1} (Known peers: {2})", senderEndPoint, senderId, string.Join(", ", _outboundSockets.Keys));
                return;
            }

            _logger.InfoFormat("Received EndOfStreamAck for {0}, {1}", senderId, senderEndPoint);

            _outboundSocketsToStop.Signal();
        }

        private void SendEndOfStreamAck(TransportMessage transportMessage)
        {
            _logger.InfoFormat("Sending EndOfStreamAck to {0}", transportMessage.Originator.SenderEndPoint);

            var endOfStreamAck = CreateInfrastructureTransportMessage(MessageTypeId.EndOfStreamAck);
            var closingPeer = new Peer(transportMessage.Originator.SenderId, transportMessage.Originator.SenderEndPoint);

            SafeAdd(_outboundSocketActions, OutboundSocketAction.Send(endOfStreamAck, new[] { closingPeer }));
            SafeAdd(_pendingDisconnects, new PendingDisconnect(closingPeer.Id, SystemDateTime.UtcNow.Add(_configuration.WaitForEndOfStreamAckTimeout)));
        }

        public TransportMessage CreateInfrastructureTransportMessage(MessageTypeId messageTypeId)
        {
            return new TransportMessage(messageTypeId, new byte[0], PeerId, InboundEndPoint, MessageId.NextId())
            {
                Environment = _environment
            };
        }

        private bool IsFromCurrentEnvironment(TransportMessage transportMessage)
        {
            if (transportMessage.Environment == null)
            {
                _logger.DebugFormat("Receiving message with null environment from  {0}", transportMessage.Originator.SenderId);
            }
            else if (transportMessage.Environment != _environment)
            {
                _logger.ErrorFormat("Receiving messages from wrong environment: {0} from {1}, discarding message type {2}", transportMessage.Environment, transportMessage.Originator.SenderEndPoint, transportMessage.MessageTypeId);
                return false;
            }
            return true;
        }

        private void OutboundProc()
        {
            Thread.CurrentThread.Name = "ZmqTransport.OutboundProc";
            _logger.DebugFormat("Starting outbound proc...");

            var outputBuffer = new MemoryStream();

            foreach (var socketAction in _outboundSocketActions.GetConsumingEnumerable())
            {
                RunOutboundSocketAction(socketAction, outputBuffer);
            }

            GracefullyDisconnectOutboundSockets(outputBuffer);

            _logger.InfoFormat("OutboundProc terminated");
        }

        private void RunOutboundSocketAction(OutboundSocketAction socketAction, MemoryStream outputBuffer)
        {
            if (socketAction.IsDisconnect)
                DisconnectPeers(socketAction.PeerIds);
            else
                SerializeAndSendTransportMessage(outputBuffer, socketAction.Message, socketAction.Peers);
        }

        private void DisconnectPeers(IEnumerable<PeerId> peerIds)
        {
            foreach (var peerId in peerIds)
            {
                ZmqOutboundSocket outboundSocket;
                if (!_outboundSockets.TryRemove(peerId, out outboundSocket))
                    continue;

                try
                {
                    outboundSocket.Disconnect();
                }
                catch (Exception ex)
                {
                    _logger.ErrorFormat("Failed to disconnect peer, PeerId: {0}, Exception: {1}", peerId, ex);
                }
            }
        }

        private void SerializeAndSendTransportMessage(MemoryStream outputBuffer, TransportMessage transportMessage, IEnumerable<Peer> targets)
        {
            Serialize(outputBuffer, transportMessage);

            foreach (var peer in targets)
            {
                try
                {
                    var outboundSocket = GetConnectedOutboundSocket(peer);
                    outboundSocket.Send(outputBuffer, transportMessage);
                }
                catch (Exception ex)
                {
                    _logger.ErrorFormat("Failed to send message, PeerId: {0}, Exception: {1}", peer.Id, ex);
                }
            }
        }

        private ZmqOutboundSocket GetConnectedOutboundSocket(Peer peer)
        {
            var outboundSocket = _outboundSockets.GetValueOrDefault(peer.Id);
            if (outboundSocket == null)
            {
                outboundSocket = new ZmqOutboundSocket(_context, peer.Id, peer.EndPoint, _socketOptions);
                outboundSocket.Connect();

                _outboundSockets.TryAdd(peer.Id, outboundSocket);
            }
            else if (outboundSocket.EndPoint != peer.EndPoint)
            {
                outboundSocket.Reconnect(peer.EndPoint);
            }

            return outboundSocket;
        }

        private void GracefullyDisconnectOutboundSockets(MemoryStream outputBuffer)
        {
            _outboundSocketsToStop = new CountdownEvent(_outboundSockets.Count);

            SendEndOfStreamMessages(outputBuffer);

            _logger.InfoFormat("Waiting for {0} outbound socket end of stream acks", _outboundSocketsToStop.InitialCount);
            if (!_outboundSocketsToStop.Wait(_configuration.WaitForEndOfStreamAckTimeout))
                _logger.WarnFormat("{0} peers did not respond to end of stream", _outboundSocketsToStop.CurrentCount);

            DisconnectPeers(_outboundSockets.Keys.ToList());
        }

        private void SendEndOfStreamMessages(MemoryStream outputBuffer)
        {
            foreach (var outboundSocket in _outboundSockets.Values)
            {
                _logger.InfoFormat("Sending EndOfStream to {0}", outboundSocket.EndPoint);

                var endOfStreamMessage = CreateInfrastructureTransportMessage(MessageTypeId.EndOfStream);
                Serialize(outputBuffer, endOfStreamMessage);
                outboundSocket.Send(outputBuffer, endOfStreamMessage);
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

                SafeAdd(_outboundSocketActions, OutboundSocketAction.Disconnect(pendingDisconnect.PeerId));
            }
        }

        private static void Serialize(MemoryStream outputBuffer, TransportMessage transportMessage)
        {
            outputBuffer.Position = 0;
            Serializer.Serialize(outputBuffer, transportMessage);
        }

        private void SafeAdd<T>(BlockingCollection<T> collection, T item)
        {
            try
            {
                collection.Add(item);
            }
            catch (Exception ex)
            {
                _logger.WarnFormat("Unable to enqueue item, Type: {0}, Exception: {1}", typeof(T).Name, ex);
            }
        }

        private struct OutboundSocketAction
        {
            private static readonly TransportMessage _disconnectMessage = new TransportMessage(null, null, new PeerId(), null, new MessageId());

            public readonly TransportMessage Message;
            public readonly IEnumerable<Peer> Peers;

            private OutboundSocketAction(TransportMessage message, IEnumerable<Peer> peers)
            {
                Message = message;
                Peers = peers;
            }

            public bool IsDisconnect
            {
                get { return Message == _disconnectMessage; }
            }

            public IEnumerable<PeerId> PeerIds
            {
                get { return Peers.Select(x => x.Id); }
            }

            public static OutboundSocketAction Send(TransportMessage message, IEnumerable<Peer> peers)
            {
                return new OutboundSocketAction(message, peers);
            }

            public static OutboundSocketAction Disconnect(PeerId peerId)
            {
                return new OutboundSocketAction(_disconnectMessage, new[] { new Peer(peerId, null) });
            }
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
            private Exception _inboundProcStartException;
            private readonly ManualResetEvent _inboundProcStartedSignal = new ManualResetEvent(false);

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

    
}