using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Abc.Zebus.Directory;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using log4net;

namespace Abc.Zebus.Persistence.Transport
{
    public class QueueingTransport : ITransport
    {
        private static readonly ILog _logger = LogManager.GetLogger(typeof(QueueingTransport));
        private readonly BlockingCollection<TransportMessage> _pendingReceives = new BlockingCollection<TransportMessage>();
        private readonly ITransport _transport;
        private readonly IPeerDirectory _peerDirectory;
        private readonly IPersistenceConfiguration _configuration;
        private Thread _receptionThread;
        private CountdownEvent _ackCountdown;

        public QueueingTransport(ITransport transport, IPeerDirectory peerDirectory, IPersistenceConfiguration configuration)
        {
            _transport = transport;
            _peerDirectory = peerDirectory;
            _configuration = configuration;
        }

        public event Action<TransportMessage> MessageReceived;

        public PeerId PeerId => _transport.PeerId;

        public string InboundEndPoint => _transport.InboundEndPoint;

        public int PendingSendCount => _transport.PendingSendCount;

        public int PendingReceiveCount => _pendingReceives.Count;

        public void Configure(PeerId peerId, string environment)
        {
            _transport.Configure(peerId, environment);
        }

        public void OnPeerUpdated(PeerId peerId, PeerUpdateAction peerUpdateAction)
        {
            _transport.OnPeerUpdated(peerId, peerUpdateAction);
        }

        public void OnRegistered()
        {
            _transport.OnRegistered();
        }

        public void Start()
        {
            _transport.MessageReceived += OnTransportMessageReceived;
            _transport.Start();

            _receptionThread = BackgroundThread.Start(PendingReceivesProcessor);
        }

        private void OnTransportMessageReceived(TransportMessage transportMessage)
        {
            if (transportMessage.MessageTypeId == MessageTypeId.PersistenceStoppingAck)
            {
                _logger.InfoFormat("Received PersistenceStoppingAck from {0}", transportMessage.Originator.SenderId);
                _ackCountdown?.Signal();
                return;
            }

            if (transportMessage.MessageTypeId.IsInfrastructure())
                MessageReceived?.Invoke(transportMessage);
            else
                _pendingReceives.TryAdd(transportMessage);
        }

        private void PendingReceivesProcessor()
        {
            Thread.CurrentThread.Name = "QueueingTransport.PendingReceivesProcessor";

            foreach (var transportMessage in _pendingReceives.GetConsumingEnumerable())
            {
                MessageReceived?.Invoke(transportMessage);
            }
        }

        public void Stop()
        {
            var targets = _peerDirectory.GetPeerDescriptors().Select(desc => desc.Peer).Where(peer => peer.Id != _transport.PeerId).ToList();
            _ackCountdown = new CountdownEvent(targets.Count);

            _transport.Send(new TransportMessage(MessageTypeId.PersistenceStopping, new MemoryStream(), PeerId, InboundEndPoint), targets, new SendContext());

            _logger.InfoFormat("Waiting for {0} persistence stopping acknowledgments within the next {1} seconds", targets.Count, _configuration.QueuingTransportStopTimeout.TotalSeconds);
            var success = _ackCountdown.Wait(_configuration.QueuingTransportStopTimeout);
            if (!success)
                _logger.WarnFormat("{0} acknowledgments not received", _ackCountdown.CurrentCount);

            var newTargetsCount = _peerDirectory.GetPeerDescriptors().Count(desc => desc.PeerId != _transport.PeerId);
            if (newTargetsCount > targets.Count)
                _logger.WarnFormat("The peer count on the bus raised from {0} to {1} during the graceful shutdown of the persistence, some messages might have been lost.", targets.Count, newTargetsCount);
            
            _logger.InfoFormat("Stopping ZmqTransport");
            _transport.Stop();

            _pendingReceives.CompleteAdding();
            if (_receptionThread != null && !_receptionThread.Join(30.Seconds()))
                _logger.WarnFormat("Unable to stop reception thread");
        }

        public void Send(TransportMessage message, IEnumerable<Peer> peerIds, SendContext sendContext)
        {
            _transport.Send(message, peerIds, sendContext);
        }

        public void AckMessage(TransportMessage transportMessage)
        {
        }
    }
}
