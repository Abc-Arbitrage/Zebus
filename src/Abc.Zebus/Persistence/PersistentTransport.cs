using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Abc.Zebus.Core;
using Abc.Zebus.Directory;
using Abc.Zebus.Serialization;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using log4net;

namespace Abc.Zebus.Persistence
{
    public partial class PersistentTransport : IPersistentTransport
    {
        private static readonly ILog _logger = LogManager.GetLogger(typeof(PersistentTransport));
        private static readonly List<MessageTypeId> _replayMessageTypeIds = new List<MessageTypeId> { MessageReplayed.TypeId, ReplayPhaseEnded.TypeId, SafetyPhaseEnded.TypeId };
        private static readonly MessageBinding _bindingForPersistence = MessageBinding.Default<PersistMessageCommand>();
        private static readonly List<Peer> _emptyPeerList = new List<Peer>();

        private readonly ConcurrentDictionary<MessageId, bool> _receivedMessagesIds = new ConcurrentDictionary<MessageId, bool>();
        private readonly BlockingCollection<TransportMessage> _messagesWaitingForPersistence = new BlockingCollection<TransportMessage>();
        private readonly IMessageSerializer _serializer = new MessageSerializer();
        private readonly IBusConfiguration _configuration;
        private readonly ITransport _innerTransport;
        private readonly IPeerDirectory _peerDirectory;
        private readonly IMessageSendingStrategy _messageSendingStrategy;
        private readonly bool _isPersistent;
        private BlockingCollection<TransportMessage> _pendingReceives = new BlockingCollection<TransportMessage>();
        private bool _isRunning;
        private Phase _phase = default!;
        private Thread? _receptionThread;
        private Guid? _currentReplayId;
        private volatile bool _persistenceIsDown;

        public PersistentTransport(IBusConfiguration configuration, ITransport innerTransport, IPeerDirectory peerDirectory, IMessageSendingStrategy messageSendingStrategy)
        {
            _configuration = configuration;
            _isPersistent = configuration.IsPersistent;
            _innerTransport = innerTransport;
            _peerDirectory = peerDirectory;
            _messageSendingStrategy = messageSendingStrategy;

            SetInitialPhase();

            _innerTransport.MessageReceived += OnTransportMessageReceived;
        }

        public event Action<TransportMessage>? MessageReceived;

        public PeerId PeerId => _innerTransport.PeerId;

        public string InboundEndPoint => _innerTransport.InboundEndPoint;

        public int PendingSendCount => _innerTransport.PendingSendCount;

        public int PendingPersistenceSendCount => _messagesWaitingForPersistence.Count;

        private void SetInitialPhase()
        {
            SetPhase(_isPersistent ? (Phase)new ReplayPhase(this) : new NoReplayPhase(this));
        }

        public void OnPeerUpdated(PeerId peerId, PeerUpdateAction peerUpdateAction)
        {
            _innerTransport.OnPeerUpdated(peerId, peerUpdateAction);

            if (!peerId.IsPersistence())
                return;

            if (peerUpdateAction == PeerUpdateAction.Started)
            {
                _persistenceIsDown = false;
                ReplayMessagesWaitingForPersistence();
            }
            else if (peerUpdateAction == PeerUpdateAction.Decommissioned)
            {
                _persistenceIsDown = true;
            }
        }

        public void OnRegistered()
        {
            _phase.OnRegistered();
        }

        private void ReplayMessagesWaitingForPersistence()
        {
            var persistencePeers = GetPersistencePeers();

            _logger.InfoFormat("Sending {0} enqueued messages to the persistence", _messagesWaitingForPersistence.Count);

            while (_messagesWaitingForPersistence.TryTake(out var messageToSend))
            {
                _innerTransport.Send(messageToSend, persistencePeers, new SendContext());
            }
        }

        private void EnqueueOrSendToPersistenceService(IMessage message)
        {
            var transportMessage = _serializer.ToTransportMessage(message, PeerId, InboundEndPoint);
            var peers = GetPersistencePeers();

            if (peers.Count == 0)
                Enqueue(transportMessage);
            else
                SendToPersistenceService(transportMessage, peers);
        }

        private void Enqueue(IMessage message)
        {
            var transportMessage = _serializer.ToTransportMessage(message, PeerId, InboundEndPoint);
            Enqueue(transportMessage);
        }

        private void Enqueue(TransportMessage transportMessage)
        {
            _logger.InfoFormat("Enqueing in temp persistence buffer: {0}", transportMessage.Id);
            _messagesWaitingForPersistence.Add(transportMessage);

            if (!_persistenceIsDown)
                ReplayMessagesWaitingForPersistence();
        }

        public void Configure(PeerId peerId, string environment)
        {
            _innerTransport.Configure(peerId, environment);
        }

        public void Start()
        {
            if (_pendingReceives.IsAddingCompleted)
                _pendingReceives = new BlockingCollection<TransportMessage>();

            _phase.OnStart();

            _innerTransport.Start();

            _isRunning = true;
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            if (_messagesWaitingForPersistence.Any())
            {
                _logger.WarnFormat("Stopping PersistenceTransport with messages waiting for persistence to come back online!");
            }

            _innerTransport.Stop();

            _pendingReceives.CompleteAdding();
            if (_receptionThread != null && !_receptionThread.Join(30.Second()))
                _logger.WarnFormat("Unable to stop reception thread");

            SetInitialPhase();
        }

        public void Send(TransportMessage message, IEnumerable<Peer> peers)
        {
            Send(message, peers, new SendContext());
        }

        public void Send(TransportMessage message, IEnumerable<Peer> peers, SendContext context)
        {
            if (context.PersistentPeerIds.Count != 0)
                throw new ArgumentException("Send invoked with non-empty send context", nameof(context));

            var isMessagePersistent = _messageSendingStrategy.IsMessagePersistent(message);
            var targetPeers = LoadTargetPeersAndUpdateContext(peers, isMessagePersistent, context);

            var mustBeSendToPersistence = context.PersistentPeerIds.Count != 0;
            context.PersistencePeer = mustBeSendToPersistence ? GetPersistencePeers().FirstOrDefault() : null;

            _innerTransport.Send(message, targetPeers, context);

            if (mustBeSendToPersistence && context.PersistencePeer == null)
                Enqueue(new PersistMessageCommand(message, context.PersistentPeerIds));
        }

        private IList<Peer> GetPersistencePeers()
        {
            return _persistenceIsDown ? _emptyPeerList : _peerDirectory.GetPeersHandlingMessage(_bindingForPersistence);
        }

        private List<Peer> LoadTargetPeersAndUpdateContext(IEnumerable<Peer> peers, bool isMessagePersistent, SendContext context)
        {
            var peerList = peers as List<Peer> ?? peers.ToList();
            var hasDownPeer = false;

            for (int index = 0; index < peerList.Count; index++)
            {
                var peer = peerList[index];
                if (isMessagePersistent && _peerDirectory.IsPersistent(peer.Id))
                    context.PersistentPeerIds.Add(peer.Id);

                hasDownPeer |= !peer.IsUp;
            }

            if (!hasDownPeer)
                return peerList;

            var targetPeers = new List<Peer>();

            for (int index = 0; index < peerList.Count; index++)
            {
                var peer = peerList[index];
                if (peer.IsUp)
                    targetPeers.Add(peer);
            }

            return targetPeers;
        }

        private void SetPhase(Phase phase)
        {
            var phaseType = phase.GetType();
            if (_phase == null)
            {
                _logger.InfoFormat("Initial phase: {0}", phaseType.Name);
            }
            else
            {
                var curentPhaseType = _phase.GetType();
                if (curentPhaseType != phaseType)
                    _logger.InfoFormat("Switching phase: {0} -> {1}", curentPhaseType.Name, phaseType.Name);
            }

            _phase = phase;
        }

        private void StartReceptionThread()
        {
            _receptionThread = BackgroundThread.Start(PendingReceivesDispatcher);
        }

        private void OnTransportMessageReceived(TransportMessage transportMessage)
        {
            if (_replayMessageTypeIds.Contains(transportMessage.MessageTypeId))
            {
                var replayEvent = (IReplayEvent)_serializer.ToMessage(transportMessage)!;
                if (replayEvent.ReplayId == _currentReplayId)
                    _phase.OnReplayEvent(replayEvent);

                return;
            }

            if (transportMessage.MessageTypeId == MessageTypeId.PersistenceStopping)
            {
                _persistenceIsDown = true;

                var ackMessage = new TransportMessage(MessageTypeId.PersistenceStoppingAck, new MemoryStream(), _innerTransport.PeerId, _innerTransport.InboundEndPoint);

                _logger.InfoFormat("Sending PersistenceStoppingAck to {0}", transportMessage.Originator.SenderId);
                _innerTransport.Send(ackMessage, new[] { new Peer(transportMessage.Originator.SenderId, transportMessage.Originator.SenderEndPoint) }, new SendContext());

                return;
            }

            if (transportMessage.MessageTypeId.IsInfrastructure())
                TriggerMessageReceived(transportMessage);
            else
                _phase.OnRealTimeMessage(transportMessage);
        }

        private void PendingReceivesDispatcher()
        {
            Thread.CurrentThread.Name = "PersistentTransport.PendingReceivesDispatcher";

            _logger.InfoFormat("Starting reception pump");

            foreach (var transportMessage in _pendingReceives.GetConsumingEnumerable())
            {
                try
                {
                    _phase.ProcessPendingReceive(transportMessage);
                }
                catch (Exception exception)
                {
                    var errorMessage = $"Unable to process message {transportMessage.MessageTypeId.FullName}. Originator: {transportMessage.Originator.SenderId}";
                    _logger.Error(errorMessage, exception);
                }
            }

            _phase.PendingReceivesProcessingCompleted();
        }

        private void TriggerMessageReceived(TransportMessage transportMessage)
        {
            MessageReceived?.Invoke(transportMessage);
        }

        public void AckMessage(TransportMessage transportMessage)
        {
            if (transportMessage.WasPersisted == true || transportMessage.WasPersisted == null && _isPersistent && _messageSendingStrategy.IsMessagePersistent(transportMessage))
            {
                _logger.DebugFormat("PERSIST ACK: {0} {1}", transportMessage.MessageTypeId, transportMessage.Id);

                EnqueueOrSendToPersistenceService(new MessageHandled(transportMessage.Id));
            }
        }

        private void SendToPersistenceService(TransportMessage transportMessage, IEnumerable<Peer> persistencePeers)
        {
            _innerTransport.Send(transportMessage, persistencePeers, new SendContext());
        }
    }
}
