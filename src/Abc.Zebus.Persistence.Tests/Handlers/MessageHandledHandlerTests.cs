using System.Collections.Generic;
using Abc.Zebus.Persistence.Handlers;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Persistence.Storage;
using Abc.Zebus.Persistence.Tests.TestUtil;
using Abc.Zebus.Persistence.Util;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.Tests.Handlers
{
    public class MessageHandledHandlerTests : HandlerTestFixture<MessageHandledHandler>
    {
        private readonly PeerId _targetPeerId = new PeerId("Abc.Testing.Target.0");
        private InMemoryMessageMatcher _inMemoryMessageMatcher;
        private IList<MatcherEntry> _persistedEntries;

        protected override void BeforeBuildingHandler()
        {
            base.BeforeBuildingHandler();
            
            var persistenceConfigurationMock = new Mock<IPersistenceConfiguration>();
            persistenceConfigurationMock.Setup(conf => conf.PersisterBatchSize).Returns(() => 100);
            
            _persistedEntries = new List<MatcherEntry>();
            var storageMock = new Mock<IStorage>();
            storageMock.Setup(x => x.Write(It.IsAny<IList<MatcherEntry>>())).Callback<IList<MatcherEntry>>(items => _persistedEntries.AddRange(items));

            _inMemoryMessageMatcher = new InMemoryMessageMatcher(persistenceConfigurationMock.Object, storageMock.Object, Bus);
            _inMemoryMessageMatcher.Start();
            MockContainer.Register<IInMemoryMessageMatcher>(_inMemoryMessageMatcher);
        }

        [Test]
        public void should_insert_message_ack()
        {
            using (SystemDateTime.PauseTime())
            {
                var transportMessageId = MessageId.NextId();
                Handler.Context = MessageContext.CreateOverride(_targetPeerId, null);
                
                Handler.Handle(new MessageHandled(transportMessageId));

                Wait.Until(() => _inMemoryMessageMatcher.CassandraInsertCount == 1, 2.Seconds());
                var persistedAck = _persistedEntries.ExpectedSingle();
                persistedAck.IsAck.ShouldBeTrue();
                persistedAck.MessageId.Value.ShouldEqual(transportMessageId.Value);
            }
        }

        [Test]
        public void should_forward_MessageHandled_to_active_replayers()
        {
            var messageReplayerMock = new Mock<IMessageReplayer>();
            MockContainer.GetMock<IMessageReplayerRepository>().Setup(x => x.GetActiveMessageReplayer(_targetPeerId)).Returns(messageReplayerMock.Object);

            Handler.Context = MessageContext.CreateOverride(_targetPeerId, null);
            var messageHandled = new MessageHandled(MessageId.NextId());
            Handler.Handle(messageHandled);

            messageReplayerMock.Verify(x => x.Handle(messageHandled));
        }

        [Test]
        public void should_not_throw_exception_if_no_replayer_for_given_MessageHandled()
        {
            Handler.Context = MessageContext.CreateOverride(_targetPeerId, null);
            
            Assert.DoesNotThrow(() => Handler.Handle(new MessageHandled(MessageId.NextId())));
        }
    }
}