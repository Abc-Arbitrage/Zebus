using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private static readonly MessageTypeId[] _replayMessageTypeIds = { MessageReplayed.TypeId, ReplayPhaseEnded.TypeId, SafetyPhaseEnded.TypeId };

        private readonly ILog _logger = LogManager.GetLogger(typeof(PersistentTransport));
        private readonly ConcurrentDictionary<MessageId, bool> _receivedMessagesIds = new ConcurrentDictionary<MessageId, bool>();
        private readonly BlockingCollection<IMessage> _messagesWaitingForPersistence = new BlockingCollection<IMessage>();
        private readonly IMessageSerializer _serializer = new MessageSerializer();
        private readonly IBusConfiguration _configuration;
        private readonly ITransport _innerTransport;
        private readonly IPeerDirectory _peerDirectory;
        private readonly IMessageSendingStrategy _messageSendingStrategy;
        private readonly bool _isPersistent;
        private BlockingCollection<TransportMessage> _pendingReceives;
        private bool _isRunning;
        private Phase _phase;
        private Thread _receptionThread;
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

        public event Action<TransportMessage> MessageReceived = delegate { };

        public PeerId PeerId
        {
            get { return _innerTransport.PeerId; }
        }

        public string InboundEndPoint
        {
            get { return _innerTransport.InboundEndPoint; }
        }

        public int PendingSendCount
        {
            get { return _innerTransport.PendingSendCount; }
        }

        public int PendingPersistenceSendCount
        {
            get { return _messagesWaitingForPersistence.Count; }
        }

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
            var persistencePeers = _peerDirectory.GetPeersHandlingMessage(MessageBinding.Default<PersistMessageCommand>());

            _logger.InfoFormat("Sending {0} enqueued messages to the persistence", _messagesWaitingForPersistence.Count);

            IMessage messageToSend;
            while (_messagesWaitingForPersistence.TryTake(out messageToSend))
                SendToPersistenceService(messageToSend, persistencePeers);
        }

        private void EnqueueOrSendToPersistenceService(IMessage message)
        {
            var peers = _peerDirectory.GetPeersHandlingMessage(message);
            if (_persistenceIsDown || peers.Count == 0)
            {
                _logger.Info("Enqueing in temp persistence buffer: " + message);
                _messagesWaitingForPersistence.Add(message);

                if (!_persistenceIsDown)
                    ReplayMessagesWaitingForPersistence();

                return;
            }

            SendToPersistenceService(message, peers);
        }

        public void Configure(PeerId peerId, string environment)
        {
            _innerTransport.Configure(peerId, environment);
        }

        public void Start()
        {
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
            if (context.PersistedPeerIds.Any())
                throw new ArgumentException("Send invoked with non-empty send context", "context");

            var isMessagePersistent = _messageSendingStrategy.IsMessagePersistent(message);
            var peerList = (peers as IList<Peer>) ?? peers.ToList();
            var upPeers = (peerList.Count == 1 && peerList[0].IsUp) ? peerList : peerList.Where(peer => peer.IsUp).ToList();

            context.PersistedPeerIds.AddRange(upPeers.Where(peer => isMessagePersistent && _peerDirectory.IsPersistent(peer.Id)).Select(x => x.Id));

            _innerTransport.Send(message, upPeers, context);
            
            if (!isMessagePersistent)
                return;

            var persistentPeerIds = peerList.Where(p => _peerDirectory.IsPersistent(p.Id)).Select(x => x.Id).ToArray();

            if (!persistentPeerIds.Any())
                return;
            
            var persistMessageCommand = new PersistMessageCommand(message, persistentPeerIds);
            EnqueueOrSendToPersistenceService(persistMessageCommand);
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
                var replayEvent = (IReplayEvent)_serializer.ToMessage(transportMessage);
                if (replayEvent.ReplayId == _currentReplayId)
                    _phase.OnReplayEvent(replayEvent);

                return;
            }

            if (transportMessage.MessageTypeId == MessageTypeId.PersistenceStopping)
            {
                _persistenceIsDown = true;

                var ackMessage = TransportMessage.Infrastructure(MessageTypeId.PersistenceStoppingAck, _innerTransport.PeerId, _innerTransport.InboundEndPoint);

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
                    var errorMessage = string.Format("Unable to process message {0}. Originator: {1}", transportMessage.MessageTypeId.FullName,
                                                     transportMessage.Originator.SenderId);
                    _logger.Error(errorMessage, exception);
                }
            }

            _phase.PendingReceivesProcessingCompleted();
        }

        private void TriggerMessageReceived(TransportMessage transportMessage)
        {
            MessageReceived(transportMessage);
        }

        public void AckMessage(TransportMessage transportMessage)
        {
            if (transportMessage.WasPersisted == true || transportMessage.WasPersisted == null && _isPersistent && _messageSendingStrategy.IsMessagePersistent(transportMessage))
            {
                _logger.DebugFormat("PERSIST ACK: {0} {1}", transportMessage.MessageTypeId, transportMessage.Id);

                EnqueueOrSendToPersistenceService(new MessageHandled(transportMessage.Id));
            }
        }

        private void SendToPersistenceService(IMessage message, IEnumerable<Peer> persistentPeers)
        {
            var transportMessage = _serializer.ToTransportMessage(message, MessageId.NextId(), PeerId, InboundEndPoint);
            _innerTransport.Send(transportMessage, persistentPeers, new SendContext());
        }
    }
}