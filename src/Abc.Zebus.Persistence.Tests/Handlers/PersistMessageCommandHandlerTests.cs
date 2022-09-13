using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.Handlers;
using Abc.Zebus.Persistence.Tests.Matching;
using Abc.Zebus.Serialization;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.Tests.Handlers
{
    [TestFixture]
    public class PersistMessageCommandHandlerTests
    {
        private PersistMessageCommandHandler _handler;
        private Mock<IMessageReplayerRepository> _replayerRepository;
        private TestInMemoryMessageMatcher _messageMatcher;
        private Mock<IPersistenceConfiguration> _configuration;

        [SetUp]
        public void SetUp()
        {
            _replayerRepository = new Mock<IMessageReplayerRepository>();
            _messageMatcher = new TestInMemoryMessageMatcher();
            _configuration = new Mock<IPersistenceConfiguration>();

            _handler = new PersistMessageCommandHandler(_replayerRepository.Object, _messageMatcher, _configuration.Object);
        }

        [Test]
        public void should_persist_message()
        {
            // Arrange
            var transportMessage = new FakeCommand(42).ToTransportMessage();
            var peerId = new PeerId("Abc.Testing.Target");

            // Act
            _handler.Handle(new PersistMessageCommand(transportMessage, peerId));

            // Assert
            var message = _messageMatcher.Messages.ExpectedSingle();
            message.peerId.ShouldEqual(peerId);
            message.messageId.ShouldEqual(transportMessage.Id);
            message.messageTypeId.ShouldEqual(transportMessage.MessageTypeId);
            message.transportMessageBytes.ShouldEqual(ProtoBufConvert.Serialize(transportMessage).ToArray());
        }

        [Test]
        public void should_ignore_message_with_empty_type()
        {
            // Arrange
            var transportMessage = new FakeCommand(42).ToTransportMessage();
            var targetPeerId = new PeerId("Abc.Testing.Target");
            transportMessage.MessageTypeId = default;

            // Act
            _handler.Handle(new PersistMessageCommand(transportMessage, targetPeerId));

            // Assert
            _messageMatcher.Messages.ShouldBeEmpty();
        }

        [Test]
        public void should_ignore_message_with_empty_target()
        {
            // Arrange
            var transportMessage = new FakeCommand(42).ToTransportMessage();
            var invalidTargetPeerId = new PeerId("");
            var targetPeerId = new PeerId("Abc.Peer.0");

            // Act
            _handler.Handle(new PersistMessageCommand(transportMessage, invalidTargetPeerId, targetPeerId));

            // Assert
            _messageMatcher.Messages.ShouldHaveSize(1);
        }

        [Test]
        public void should_send_message_to_replayer()
        {
            // Arrange
            var targetPeerId = new PeerId("Abc.Testing.Target");
            var replayerMock = new Mock<IMessageReplayer>();
            _replayerRepository.Setup(x => x.GetActiveMessageReplayer(targetPeerId)).Returns(replayerMock.Object);

            var message = new FakeCommand(1);
            var transportMessage = message.ToTransportMessage();
            transportMessage.GetContentBytes().ShouldEqual(ProtoBufConvert.Serialize(message).ToArray());

            // Act
            _handler.Handle(new PersistMessageCommand(transportMessage, targetPeerId));

            // Assert
            replayerMock.Verify(x => x.AddLiveMessage(transportMessage));
            transportMessage.GetContentBytes().ShouldEqual(ProtoBufConvert.Serialize(message).ToArray());
        }

        [Test, Repeat(5)]
        public void should_send_message_to_multiple_replayers()
        {
            // Arrange
            const int targetPeerCount = 100;

            var replayedMessages = new List<(PeerId, TransportMessage)>();

            var targetPeerIds = Enumerable.Range(0, targetPeerCount).Select(x => new PeerId($"Abc.Testing.Target.{x}")).ToList();
            var replayers = targetPeerIds.ToDictionary(x => x, x => new TestMessageReplayer(x, replayedMessages));

            _replayerRepository.Setup(x => x.GetActiveMessageReplayer(It.IsAny<PeerId>()))
                               .Returns<PeerId>(peerId => replayers[peerId]);

            var sourceMessage = new FakeCommand(123456);
            var sourceTransportMessage = sourceMessage.ToTransportMessage().ConvertToPersistTransportMessage(targetPeerIds);
            var persistMessageCommand = (PersistMessageCommand)sourceTransportMessage.ToMessage();

            // Act
            _handler.Handle(persistMessageCommand);

            // Assert
            Wait.Until(() => replayedMessages.Count == targetPeerCount, 5.Seconds());

            foreach (var (peerId, replayedMessage) in replayedMessages)
            {
                var messageReplayed = (MessageReplayed)replayedMessage.ToMessage();
                var sourceMessageCopy = (FakeCommand)messageReplayed.Message.ToMessage();
                sourceMessageCopy.ShouldEqualDeeply(sourceMessage);
            }
        }

        private class TestMessageReplayer : IMessageReplayer
        {
            private readonly MessageSerializer _messageSerializer = new MessageSerializer();
            private readonly PeerId _peerId;
            private readonly List<(PeerId, TransportMessage)> _replayMessages;

            public TestMessageReplayer(PeerId peerId, List<(PeerId, TransportMessage)> replayMessages)
            {
                _peerId = peerId;
                _replayMessages = replayMessages;
            }

            public event Action Stopped;

            public void AddLiveMessage(TransportMessage message)
            {
                Task.Run(() =>
                {
                    var messageReplayed = new MessageReplayed(Guid.Empty, message);
                    var messageReplayedTransportMessage = ToTransportMessage(messageReplayed);

                    lock (_replayMessages)
                    {
                        _replayMessages.Add((_peerId, messageReplayedTransportMessage));
                    }
                });
            }

            private TransportMessage ToTransportMessage(IMessage message)
            {
                return new TransportMessage(message.TypeId(), _messageSerializer.Serialize(message), new PeerId("Abc.Testing.0"), "tcp://testing:1234");
            }

            public void Start()
            {
            }

            public bool Cancel()
            {
                Stopped?.Invoke();
                return true;
            }

            public void OnMessageAcked(MessageId messageId)
            {
            }
        }
    }
}
