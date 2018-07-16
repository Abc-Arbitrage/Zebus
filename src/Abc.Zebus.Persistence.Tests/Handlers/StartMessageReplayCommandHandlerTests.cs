using System;
using Abc.Zebus.Persistence.Handlers;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.Tests.Handlers
{
    [TestFixture]
    public class StartMessageReplayCommandHandlerTests
    {
        private StartMessageReplayCommandHandler _handler;
        private Mock<IMessageReplayerRepository> _messageReplayerRepositoryMock;
        private Peer _sender;

        [SetUp]
        public void Setup()
        {
            _sender = new Peer(new PeerId("Abc.Testing.Sender.0"), "tcp://abctest:123");

            _messageReplayerRepositoryMock = new Mock<IMessageReplayerRepository>();
            _handler = new StartMessageReplayCommandHandler(_messageReplayerRepositoryMock.Object)
            {
                Context = MessageContext.CreateOverride(_sender.Id, _sender.EndPoint),
            };

            _messageReplayerRepositoryMock.Setup(x => x.CreateMessageReplayer(It.IsAny<Peer>(), It.IsAny<Guid>())).Returns(new Mock<IMessageReplayer>().Object);
        }

        [Test]
        public void should_add_and_start_replayer()
        {
            var replayId = Guid.NewGuid();

            var messageReplayerMock = new Mock<IMessageReplayer>();
            _messageReplayerRepositoryMock.Setup(x => x.CreateMessageReplayer(It.Is<Peer>(p => p.Id == _sender.Id && p.EndPoint == _sender.EndPoint), replayId)).Returns(messageReplayerMock.Object);

            var command = new StartMessageReplayCommand(replayId);
            _handler.Handle(command);

            messageReplayerMock.Verify(x => x.Start());
            _messageReplayerRepositoryMock.Verify(x => x.SetActiveMessageReplayer(_sender.Id, messageReplayerMock.Object));
        }

        [Test]
        public void should_stop_previous_replayer()
        {
            var previousMessageReplayerMock = new Mock<IMessageReplayer>();
            _messageReplayerRepositoryMock.Setup(x => x.GetActiveMessageReplayer(_sender.Id)).Returns(previousMessageReplayerMock.Object);

            var command = new StartMessageReplayCommand(Guid.NewGuid());
            _handler.Handle(command);

            previousMessageReplayerMock.Verify(x => x.Cancel());
        }
    }
}