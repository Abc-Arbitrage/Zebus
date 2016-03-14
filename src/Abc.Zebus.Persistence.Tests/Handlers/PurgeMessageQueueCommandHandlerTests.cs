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
        public void should_call_purge_on_storage()
        {
            Handler.Handle(new PurgeMessageQueueCommand("PeerId"));

            MockContainer.GetMock<IStorage>().Verify(x => x.PurgeMessagesAndAcksForPeer(new PeerId("PeerId")));
        }
    }
}