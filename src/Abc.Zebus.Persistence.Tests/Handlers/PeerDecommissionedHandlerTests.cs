using Abc.Zebus.Directory;
using Abc.Zebus.Persistence.Handlers;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Persistence.Tests.TestUtil;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.Tests.Handlers
{
    public class PeerDecommissionedHandlerTests : HandlerTestFixture<PeerDecommissionedHandler>
    {
        [Test]
        public void should_remove_messages_from_storage_on_decommission()
        {
            var peerDecommissioned = new PeerDecommissioned(new PeerId("Abc.Peer.Id"));

            Handler.Handle(peerDecommissioned);

            Bus.ExpectExactly(new PurgeMessageQueueCommand(peerDecommissioned.PeerId.ToString()));
        }
    }
}