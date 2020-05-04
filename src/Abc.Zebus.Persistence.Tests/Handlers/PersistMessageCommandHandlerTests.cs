using Abc.Zebus.Persistence.Handlers;
using Abc.Zebus.Persistence.Tests.Matching;
using Abc.Zebus.Serialization;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
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
            message.transportMessageBytes.ShouldEqual(Serializer.Serialize(transportMessage).ToArray());
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

            var transportMessage = new FakeCommand(1).ToTransportMessage();

            // Act
            _handler.Handle(new PersistMessageCommand(transportMessage, targetPeerId));

            // Assert
            replayerMock.Verify(x => x.AddLiveMessage(transportMessage));
        }
    }
}
