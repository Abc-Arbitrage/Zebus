using Abc.Zebus.Persistence.Handlers;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Persistence.Tests.TestUtil;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.Tests.Handlers
{
    public class MessageHandledHandlerTests : HandlerTestFixture<MessageHandledHandler>
    {
        private readonly PeerId _targetPeerId = new PeerId("Abc.Testing.Target.0");

        [Test]
        public void should_insert_message_ack_for_handled_message()
        {
            var inMemoryMessageMatcher = MockContainer.GetMock<IInMemoryMessageMatcher>();

            var messageId = MessageId.NextId();

            Handler.Context = MessageContext.CreateOverride(_targetPeerId, null);
            Handler.Handle(new MessageHandled(messageId));

            inMemoryMessageMatcher.Verify(x => x.EnqueueAck(_targetPeerId, messageId));
        }

        [Test]
        public void should_forward_handled_message_to_active_replayers()
        {
            var messageReplayerMock = new Mock<IMessageReplayer>();
            MockContainer.GetMock<IMessageReplayerRepository>().Setup(x => x.GetActiveMessageReplayer(_targetPeerId)).Returns(messageReplayerMock.Object);

            var messageId = MessageId.NextId();

            Handler.Context = MessageContext.CreateOverride(_targetPeerId, null);
            Handler.Handle(new MessageHandled(messageId));

            messageReplayerMock.Verify(x => x.OnMessageAcked(messageId));
        }

        [Test]
        public void should_insert_message_ack_for_removed_message()
        {
            var inMemoryMessageMatcher = MockContainer.GetMock<IInMemoryMessageMatcher>();

            var messageId = MessageId.NextId();

            Handler.Handle(new RemoveMessageFromQueueCommand(_targetPeerId, messageId));

            inMemoryMessageMatcher.Verify(x => x.EnqueueAck(_targetPeerId, messageId));
        }

        [Test]
        public void should_forward_removed_message_to_active_replayers()
        {
            var messageReplayerMock = new Mock<IMessageReplayer>();
            MockContainer.GetMock<IMessageReplayerRepository>().Setup(x => x.GetActiveMessageReplayer(_targetPeerId)).Returns(messageReplayerMock.Object);

            var messageId = MessageId.NextId();

            Handler.Handle(new RemoveMessageFromQueueCommand(_targetPeerId, messageId));

            messageReplayerMock.Verify(x => x.OnMessageAcked(messageId));
        }

        [Test]
        public void should_not_throw_exception_if_no_replayer_for_given_MessageHandled()
        {
            Handler.Context = MessageContext.CreateOverride(_targetPeerId, null);

            Assert.DoesNotThrow(() => Handler.Handle(new MessageHandled(MessageId.NextId())));
        }
    }
}
