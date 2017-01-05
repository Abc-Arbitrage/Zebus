using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Abc.Zebus.Directory;
using Abc.Zebus.Serialization.Protobuf;
using Abc.Zebus.Util;
using log4net;
using ZeroMQ;

namespace Abc.Zebus.Transport
{
    public class ZmqTransport : ITransport
    {
        private readonly IZmqTransportConfiguration _configuration;
        private readonly ZmqEndPoint _configuredInboundEndPoint;
        private ILog _logger = LogManager.GetLogger(typeof(ZmqTransport));
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
            var resourceName = $"libzmq-{platform}-0.0.0.0.dll";

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

        public ZmqTransport(IZmqTransportConfiguration configuration, ZmqSocketOptions socketOptions)
        {
            _configuration = configuration;
            _configuredInboundEndPoint = new ZmqEndPoint(configuration.InboundEndPoint);
            SocketOptions = socketOptions;
        }

        public event Action<TransportMessage> MessageReceived = delegate { };

        public virtual bool IsListening
        {
            get { return _isListening; }
            internal set { _isListening = value; }
        }

        public string InboundEndPoint => _realInboundEndPoint != null ? _realInboundEndPoint.Value : _configuredInboundEndPoint.Value;

        public int PendingSendCount => _outboundSocketActions?.Count ?? 0;

        public ZmqSocketOptions SocketOptions { get; }

        public int OutboundSocketCount => _outboundSockets.Count;

        public PeerId PeerId { get; private set; }

        internal void SetLogId(int logId)
        {
            _logger = LogManager.GetLogger(typeof(ZmqTransport).FullName + "#" + logId);
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
            Stop(false);
        }

        public void Stop(bool discardPendingMessages)
        {
            if (!_isRunning)
                return;

            _pendingDisconnects.CompleteAdding();

            if (discardPendingMessages)
                DiscardItems(_pendingDisconnects);

            if (!_disconnectThread.Join(30.Seconds()))
                _logger.Error("Unable to terminate disconnect thread");

            _outboundSocketActions.CompleteAdding();

            if (discardPendingMessages)
                DiscardItems(_outboundSocketActions);

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

        private static void DiscardItems<T>(BlockingCollection<T> collection)
        {
            T item;
            while (collection.TryTake(out item))
            {
            }
        }

        public void Send(TransportMessage message, IEnumerable<Peer> peers)
        {
            Send(message, peers, new SendContext());
        }

        public void Send(TransportMessage message, IEnumerable<Peer> peers, SendContext context)
        {
            _outboundSocketActions.Add(OutboundSocketAction.Send(message, peers, context));
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
                while (IsListening)
                {
                    var inputStream = inboundSocket.Receive();
                    if (inputStream == null)
                        continue;

                    DeserializeAndForwardTransportMessage(inputStream);
                }

                GracefullyDisconnectInboundSocket(inboundSocket);
            }

            _logger.InfoFormat("InboundProc terminated");
        }

        private ZmqInboundSocket CreateInboundSocket(InboundProcStartSequenceState state)
        {
            ZmqInboundSocket inboundSocket = null;
            try
            {
                inboundSocket = new ZmqInboundSocket(_context, PeerId, _configuredInboundEndPoint, SocketOptions, _environment);
                _realInboundEndPoint = inboundSocket.Bind();
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

            CodedInputStream inputStream;
            while ((inputStream = inboundSocket.Receive(100.Milliseconds())) != null)
                DeserializeAndForwardTransportMessage(inputStream);
        }

        private void DeserializeAndForwardTransportMessage(CodedInputStream inputStream)
        {
            try
            {
                var transportMessage = inputStream.ReadTransportMessage();

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

            var endOfStreamAck = TransportMessage.Infrastructure(MessageTypeId.EndOfStreamAck, PeerId, InboundEndPoint);
            var closingPeer = new Peer(transportMessage.Originator.SenderId, transportMessage.Originator.SenderEndPoint);

            SafeAdd(_outboundSocketActions, OutboundSocketAction.Send(endOfStreamAck, new[] { closingPeer }, new SendContext()));
            SafeAdd(_pendingDisconnects, new PendingDisconnect(closingPeer.Id, SystemDateTime.UtcNow.Add(_configuration.WaitForEndOfStreamAckTimeout)));
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

            var outputStream = new CodedOutputStream();

            foreach (var socketAction in _outboundSocketActions.GetConsumingEnumerable())
            {
                RunOutboundSocketAction(socketAction, outputStream);
            }

            GracefullyDisconnectOutboundSockets(outputStream);

            _logger.InfoFormat("OutboundProc terminated");
        }

        private void RunOutboundSocketAction(OutboundSocketAction socketAction, CodedOutputStream outputStream)
        {
            if (socketAction.IsDisconnect)
            {
                DisconnectPeers(socketAction.PeerIds);
            }
            else
            {
                SerializeAndSendTransportMessage(outputStream, socketAction.Message, socketAction.GetPersistentPeer().ToList(), true);
                SerializeAndSendTransportMessage(outputStream, socketAction.Message, socketAction.GetTransientPeers().ToList(), false);
            }
        }

        private void DisconnectPeers(IEnumerable<PeerId> peerIds)
        {
            foreach (var peerId in peerIds)
            {
                ZmqOutboundSocket outboundSocket;
                if (!_outboundSockets.TryRemove(peerId, out outboundSocket))
                    continue;

                outboundSocket.Disconnect();
            }
        }

        private void SerializeAndSendTransportMessage(CodedOutputStream outputStream, TransportMessage transportMessage, List<Peer> peers, bool wasPersisted)
        {
            if (!peers.Any())
                return;

            Serialize(outputStream, transportMessage, wasPersisted);

            foreach (var peer in peers)
            {
                var outboundSocket = GetConnectedOutboundSocket(peer, transportMessage);
                if (!outboundSocket.IsConnected)
                    continue;

                try
                {
                    outboundSocket.Send(outputStream.Buffer, outputStream.Position, transportMessage);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to send message, PeerId: {peer.Id}, EndPoint: {peer.EndPoint}, Exception: {ex}");
                }
            }
        }

        private ZmqOutboundSocket GetConnectedOutboundSocket(Peer peer, TransportMessage transportMessage)
        {
            ZmqOutboundSocket outboundSocket;
            if (!_outboundSockets.TryGetValue(peer.Id, out outboundSocket))
            {
                outboundSocket = new ZmqOutboundSocket(_context, peer.Id, peer.EndPoint, SocketOptions);
                outboundSocket.ConnectFor(transportMessage);

                _outboundSockets.TryAdd(peer.Id, outboundSocket);
            }
            else if (outboundSocket.EndPoint != peer.EndPoint)
            {
                outboundSocket.ReconnectFor(peer.EndPoint, transportMessage);
            }

            return outboundSocket;
        }

        private void GracefullyDisconnectOutboundSockets(CodedOutputStream outputStream)
        {
            var connectedOutboundSockets = _outboundSockets.Values.Where(x => x.IsConnected).ToList();

            _outboundSocketsToStop = new CountdownEvent(connectedOutboundSockets.Count);

            SendEndOfStreamMessages(connectedOutboundSockets, outputStream);

            _logger.InfoFormat("Waiting for {0} outbound socket end of stream acks", _outboundSocketsToStop.InitialCount);
            if (!_outboundSocketsToStop.Wait(_configuration.WaitForEndOfStreamAckTimeout))
                _logger.WarnFormat("{0} peers did not respond to end of stream", _outboundSocketsToStop.CurrentCount);

            DisconnectPeers(connectedOutboundSockets.Select(x => x.PeerId).ToList());
        }

        private void SendEndOfStreamMessages(List<ZmqOutboundSocket> connectedOutboundSockets, CodedOutputStream outputStream)
        {
            foreach (var outboundSocket in connectedOutboundSockets)
            {
                _logger.InfoFormat("Sending EndOfStream to {0}", outboundSocket.EndPoint);

                var endOfStreamMessage = TransportMessage.Infrastructure(MessageTypeId.EndOfStream, PeerId, InboundEndPoint);
                Serialize(outputStream, endOfStreamMessage, false);
                outboundSocket.Send(outputStream.Buffer, outputStream.Position, endOfStreamMessage);
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

        private void Serialize(CodedOutputStream outputStream, TransportMessage transportMessage, bool wasPersisted)
        {
            outputStream.Position = 0;

            transportMessage.Environment = _environment;
            transportMessage.WasPersisted = wasPersisted;

            outputStream.WriteTransportMessage(transportMessage);
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
            private readonly IEnumerable<Peer> _targets;
            private readonly SendContext _context;

            public readonly TransportMessage Message;

            private OutboundSocketAction(TransportMessage message, IEnumerable<Peer> targets, SendContext context)
            {
                Message = message;
                _targets = targets;
                _context = context;
            }

            public bool IsDisconnect => Message == _disconnectMessage;

            public IEnumerable<PeerId> PeerIds
            {
                get { return _targets.Select(x => x.Id); }
            }

            public IEnumerable<Peer> GetTransientPeers()
            {
                foreach (var target in _targets)
                {
                    if (!_context.PersistedPeerIds.Contains(target.Id))
                        yield return target;
                }
            }

            public IEnumerable<Peer> GetPersistentPeer()
            {
                foreach (var target in _targets)
                {
                    if (_context.PersistedPeerIds.Contains(target.Id))
                        yield return target;
                }
            }

            public static OutboundSocketAction Send(TransportMessage message, IEnumerable<Peer> peers, SendContext context)
                => new OutboundSocketAction(message, peers, context);

            public static OutboundSocketAction Disconnect(PeerId peerId)
                => new OutboundSocketAction(_disconnectMessage, new[] { new Peer(peerId, null) }, null);
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
