using System;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Persistence.Storage;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Transport;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.Tests
{
    [TestFixture]
    public class MessageReplayerRepositoryTests
    {
        private MessageReplayerRepository _repository;
        private Peer _peer;

        [SetUp]
        public void Setup()
        {
            var persistenceConfigurationMock = new Mock<IPersistenceConfiguration>();
            var transportMock = new Mock<ITransport>();
            var batchPersisterMock = new Mock<IInMemoryMessageMatcher>();
            var storage = new Mock<IStorage>();
            var speedReporter = new Mock<IReplaySpeedReporter>();

            _repository = new MessageReplayerRepository(persistenceConfigurationMock.Object, storage.Object, new TestBus(), transportMock.Object, batchPersisterMock.Object, speedReporter.Object);

            _peer = new Peer(new PeerId("Abc.Testing.Peer.0"), "tcp://abctest:888");
        }

        [Test]
        public void should_create_replayer()
        {
            var replayer = _repository.CreateMessageReplayer(_peer, Guid.NewGuid());
            replayer.ShouldBeOfType<MessageReplayer>();
        }

        [Test]
        public void should_set_and_get_message_replayer()
        {
            var replayerMock = new Mock<IMessageReplayer>();
            _repository.SetActiveMessageReplayer(_peer.Id, replayerMock.Object);

            var replayer = _repository.GetActiveMessageReplayer(_peer.Id);
            replayer.ShouldEqual(replayerMock.Object);
        }

        [Test]
        public void should_not_create_replayer_when_deactivated()
        {
            _repository.DeactivateMessageReplayers();
            Assert.Throws<InvalidOperationException>(() => _repository.CreateMessageReplayer(_peer, Guid.NewGuid()));
        }

        [Test]
        public void should_not_set_message_replayer_when_deactivated()
        {
            var replayerMock = new Mock<IMessageReplayer>();

            _repository.DeactivateMessageReplayers();
            Assert.Throws<InvalidOperationException>(() => _repository.SetActiveMessageReplayer(_peer.Id, replayerMock.Object));
        }

        [Test]
        public void should_not_get_stopped_replayer()
        {
            var replayerMock = new Mock<IMessageReplayer>();
            _repository.SetActiveMessageReplayer(_peer.Id, replayerMock.Object);
            _repository.HasActiveMessageReplayers().ShouldBeTrue();

            replayerMock.Raise(x => x.Stopped += null);

            var replayer = _repository.GetActiveMessageReplayer(_peer.Id);
            replayer.ShouldBeNull();

            _repository.HasActiveMessageReplayers().ShouldBeFalse();
        }
    }
}