using Abc.Zebus.Persistence.Handlers;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Persistence.Storage;
using Abc.Zebus.Persistence.Tests.TestUtil;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.Tests.Handlers
{
    public class PurgeMessageQueueCommandHandlerTests : HandlerTestFixture<PurgeMessageQueueCommandHandler>
    {
        [Test]
        public void should_remove_peer()
        {
            Handler.Handle(new PurgeMessageQueueCommand("PeerId"));

            MockContainer.GetMock<IStorage>().Verify(x => x.RemovePeer(new PeerId("PeerId")));
        }

        [Test]
        public void should_publish_non_ack_message_count_update()
        {
            Handler.Handle(new PurgeMessageQueueCommand("PeerId"));

            Bus.Expect(new NonAckMessagesCountChanged(new[] { new NonAckMessage("PeerId", 0) }));
        }
    }
}
