using Abc.Zebus.Persistence.Handlers;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Persistence.Tests.TestUtil;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.Tests.Handlers
{
    public class MessageHandledHandlerTests : HandlerTestFixture<MessageHandledHandler>
    {
        private readonly PeerId _targetPeerId = new PeerId("Abc.Testing.Target.0");

        [Test]
        public void should_insert_message_ack()
        {
            var inMemoryMessageMatcher = MockContainer.GetMock<IInMemoryMessageMatcher>();

            var messageId = MessageId.NextId();
            Handler.Context = MessageContext.CreateOverride(_targetPeerId, null);
                
            Handler.Handle(new MessageHandled(messageId));

            inMemoryMessageMatcher.Verify(x => x.EnqueueAck(_targetPeerId, messageId));
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