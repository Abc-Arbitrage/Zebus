using System.Collections.Generic;
using Abc.Zebus.Persistence.Handlers;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Persistence.Storage;
using Abc.Zebus.Testing;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.Tests.Handlers
{
    [TestFixture]
    public class PublishNonAckMessagesCountCommandHandlerTests
    {
        private PublishNonAckMessagesCountCommandHandler _handler;
        private Mock<IStorage> _storage;
        private TestBus _bus;

        [SetUp]
        public void SetUp()
        {
            _storage = new Mock<IStorage>();
            _bus = new TestBus();
            _handler = new PublishNonAckMessagesCountCommandHandler(_storage.Object, _bus);
        }

        [Test]
        public void should_publish_messages_count()
        {
            _storage.Setup(x => x.GetNonAckedMessageCounts())
                    .Returns(new Dictionary<PeerId, int> { { new PeerId("Abc.Peer.0"), 42 } });

            _handler.Handle(new PublishNonAckMessagesCountCommand());

            _bus.ExpectExactly(new NonAckMessagesCountChanged(new[] { new NonAckMessage("Abc.Peer.0", 42) }));
        }

        [Test]
        public void should_publish_messages_for_updated_peers()
        {
            _storage.Setup(x => x.GetNonAckedMessageCounts())
                    .Returns(new Dictionary<PeerId, int>());
            _handler.Handle(new PublishNonAckMessagesCountCommand());

            _storage.Setup(x => x.GetNonAckedMessageCounts())
                    .Returns(new Dictionary<PeerId, int> { { new PeerId("Abc.Peer.0"), 42 } }); 
            _handler.Handle(new PublishNonAckMessagesCountCommand());

            _storage.Setup(x => x.GetNonAckedMessageCounts())
                    .Returns(new Dictionary<PeerId, int> { { new PeerId("Abc.Peer.0"), 43 } }); 
            _handler.Handle(new PublishNonAckMessagesCountCommand());

            _bus.ExpectExactly(new NonAckMessagesCountChanged(new NonAckMessage[0]),
                               new NonAckMessagesCountChanged(new[] { new NonAckMessage("Abc.Peer.0", 42) }),
                               new NonAckMessagesCountChanged(new[] { new NonAckMessage("Abc.Peer.0", 43) }));
        }
    }
}
