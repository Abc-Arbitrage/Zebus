using System.Collections.Generic;
using Abc.Zebus.Persistence.Handlers;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Persistence.Storage;
using Abc.Zebus.Persistence.Tests.TestUtil;
using Abc.Zebus.Persistence.Util;
using Abc.Zebus.Testing;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.Tests.Handlers
{
    public class PersistMessageCommandHandlerTests : HandlerTestFixture<PersistMessageCommandHandler>
    {
        private InMemoryMessageMatcher _inMemoryMessageMatcher;
        private Mock<IStorage> _storageMock;

        protected override void BeforeBuildingHandler()
        {
            base.BeforeBuildingHandler();

            _storageMock = new Mock<IStorage>();
            var persistenceConfigurationMock = new Mock<IPersistenceConfiguration>();
            persistenceConfigurationMock.SetupGet(conf => conf.PersisterBatchSize).Returns(() => 100);

            _inMemoryMessageMatcher = new InMemoryMessageMatcher(persistenceConfigurationMock.Object, _storageMock.Object, Bus);
            _inMemoryMessageMatcher.Start();
            MockContainer.Register<IInMemoryMessageMatcher>(_inMemoryMessageMatcher);
        }

        [TearDown]
        public override void Teardown()
        {
            _inMemoryMessageMatcher.Stop();

            base.Teardown();
        }

        [Test]
        public void should_persist_message()
        {
            using (SystemDateTime.PauseTime())
            {
                var transportMessage = new FakeCommand(42).ToTransportMessage();

                var peerId = new PeerId("Abc.Testing.Target");
                var command = new PersistMessageCommand(transportMessage, new[] { peerId });
                Handler.Handle(command);

                Wait.Until(() => _inMemoryMessageMatcher.CassandraInsertCount == 1, 2.Seconds());
                _storageMock.Verify(x=>x.Write(It.IsAny<IList<MatcherEntry>>()), Times.Once);
            }
        }
        [Test]
        public void should_send_message_to_replayer()
        {
            var targetPeer = new PeerId("Abc.Testing.Target");
            var replayerMock = new Mock<IMessageReplayer>();
            MockContainer.GetMock<IMessageReplayerRepository>().Setup(x => x.GetActiveMessageReplayer(targetPeer)).Returns(replayerMock.Object);
            var transportMessage = new FakeCommand(1).ToTransportMessage();
            var command = new PersistMessageCommand(transportMessage, new[] { targetPeer });
            
            Handler.Handle(command);

            replayerMock.Verify(x => x.AddLiveMessage(transportMessage));
        }

       
    }
}