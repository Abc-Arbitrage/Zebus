using Abc.Zebus.Directory;
using Abc.Zebus.Persistence.Handlers;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.Tests.Handlers
{
    [TestFixture]
    public class PeerStoppedHandlerTests
    {
        private PeerStoppedHandler _handler;
        private Mock<IMessageReplayerRepository> _replayerRepositoryMock;

        [SetUp]
        public void Setup()
        {
            _replayerRepositoryMock = new Mock<IMessageReplayerRepository>();
            _handler = new PeerStoppedHandler(_replayerRepositoryMock.Object);
        }

        [Test]
        public void should_cancel_replayer()
        {
            var peerId = new PeerId("Abc.Testing.0");
            var replayerMock = new Mock<IMessageReplayer>();
            _replayerRepositoryMock.Setup(x => x.GetActiveMessageReplayer(peerId)).Returns(replayerMock.Object);

            _handler.Handle(new PeerStopped(peerId, "tcp://x:1234"));

            replayerMock.Verify(x => x.Cancel());
        }
    }
}