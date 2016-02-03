using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Directory;
using Abc.Zebus.Serialization;
using Abc.Zebus.Testing.Comparison;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Transport;
using NUnit.Framework;

namespace Abc.Zebus.Testing.Transport
{
    public class TestTransport : ITransport
    {
        private readonly List<TransportMessageSent> _messages = new List<TransportMessageSent>();
        private readonly List<UpdatedPeer> _updatedPeers = new List<UpdatedPeer>();
        private readonly List<TransportMessage> _ackedMessages = new List<TransportMessage>();
        private readonly MessageSerializer _messageSerializer = new MessageSerializer();

        public TestTransport(Peer peer, string environment)
        {
            InboundEndPoint = peer.EndPoint;
            Configure(peer.Id, environment);
            MessagesSent = new List<IMessage>();
        }

        public TestTransport(string inboundEndPoint = "tcp://in-memory-test:1234")
        {
            InboundEndPoint = inboundEndPoint;
            MessagesSent = new List<IMessage>();
        }

        public event Action<TransportMessage> MessageReceived = delegate { };
        public event Action Started = delegate { };
        public event Action Registered = delegate { };

        public PeerId PeerId { get; private set; }
        public string InboundEndPoint { get; private set; }
        public int PendingSendCount { get; set; }
        public IEnumerable<UpdatedPeer> UpdatedPeers { get { return _updatedPeers; } }
        public bool IsStopped { get; private set; }
        public bool IsStarted { get; private set; }
        public bool IsConfigured { get; private set; }
        public List<IMessage> MessagesSent { get; private set; }
        public bool IsRegistered { get; private set; }

        public void Configure(PeerId peerId, string environment)
        {
            PeerId = peerId;
            IsConfigured = true;
        }

        public void OnPeerUpdated(PeerId peerId, PeerUpdateAction peerUpdateAction)
        {
            _updatedPeers.Add(new UpdatedPeer(peerId, peerUpdateAction));
        }

        public void OnRegistered()
        {
            Registered();
            IsRegistered = true;
        }

        public void Start()
        {
            Started();
            IsStarted = true;
        }

        public void Stop()
        {
            IsStopped = true;
        }

        public void Send(TransportMessage message, IEnumerable<Peer> peers)
        {
            Send(message, peers, new SendContext());
        }

        public void Send(TransportMessage message, IEnumerable<Peer> peers, SendContext context)
        {
            var peerList = peers.ToList();
            if (peerList.Any())
                _messages.Add(new TransportMessageSent(message, peerList, context));

            var deserializedMessage = _messageSerializer.Deserialize(message.MessageTypeId, message.MessageBytes);
            if (deserializedMessage != null)
                MessagesSent.Add(deserializedMessage);
        }

        public void AckMessage(TransportMessage transportMessage)
        {
            _ackedMessages.Add(transportMessage);
        }

        public TransportMessage CreateInfrastructureTransportMessage(MessageTypeId messageTypeId)
        {
            return new TransportMessage(messageTypeId, new byte[0], PeerId, InboundEndPoint, MessageId.NextId());
        }

        public void RaiseMessageReceived(TransportMessage transportMessage)
        {
            MessageReceived(transportMessage);
        }

        public IList<TransportMessageSent> Messages
        {
            get { return _messages; }
        }

        public IList<TransportMessage> AckedMessages
        {
            get { return _ackedMessages; }
        }

        public void ExpectExactly(params TransportMessageSent[] expectedMessages)
        {
            var comparer = new MessageComparer();
            comparer.CheckExpectations(Messages, expectedMessages, true);
        }

        public void ExpectNothing()
        {
            NUnitExtensions.ShouldBeEmpty(Messages, "Messages not empty. Content:" + Environment.NewLine + string.Join(Environment.NewLine, Messages.Select(msg => msg.TransportMessage.MessageTypeId.GetMessageType().Name)));
        }

        public void Expect(params TransportMessageSent[] expectedMessages)
        {
            var comparer = new MessageComparer();
            comparer.CheckExpectations(Messages, expectedMessages, false);
        }

        public void ExpectNot(params TransportMessageSent[] notExpectedMessages)
        {
            var comparer = ComparisonExtensions.CreateComparer();
            foreach (var notExpectedMessage in notExpectedMessages)
            {
                var matchingMessage = Messages.FirstOrDefault(x => comparer.Compare(notExpectedMessage, x).AreEqual);
                if (matchingMessage != null)
                    Assert.Fail("Found message matching " + notExpectedMessage.TransportMessage.MessageTypeId.GetMessageType().Name);
            }
        }
    }
}