using System;
using System.Linq;
using Abc.Zebus.Directory.Configuration;
using Abc.Zebus.Directory.Handlers;
using Abc.Zebus.Directory.Storage;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Directory.Tests.Handlers
{
    [TestFixture]
    public class DirectoryCommandsHandlerTests
    {
        private readonly Peer _sender = new Peer(new PeerId("Abc.Sender.0"), "tcp://sender:123");
        private IDisposable _contextScope;
        private TestBus _bus;
        private Mock<IPeerRepository> _repositoryMock;
        private Mock<IDirectoryConfiguration> _configurationMock;
        private DirectoryCommandsHandler _handler;

        [SetUp]
        public void Setup()
        {
            _contextScope = MessageContext.SetCurrent(MessageContext.CreateTest());

            _configurationMock = new Mock<IDirectoryConfiguration>();
            _configurationMock.SetupGet(conf => conf.BlacklistedMachines).Returns(new[] { "ANOTHER_BLACKLISTEDMACHINE", "BLACKlistedMACHINE" });
            _repositoryMock = new Mock<IPeerRepository>();
            _bus = new TestBus();
            _handler = new DirectoryCommandsHandler(_bus, _repositoryMock.Object, _configurationMock.Object) { Context = MessageContext.CreateOverride(_sender.Id, _sender.EndPoint) };
        }

        [TearDown]
        public virtual void Teardown()
        {
            _contextScope.Dispose();
        }

        [Test]
        public void should_add_peer_to_repository()
        {
            var peerDescriptor = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://abctest:123", typeof(FakeCommand));
            var registerCommand = new RegisterPeerCommand(peerDescriptor);

            _handler.Handle(registerCommand);

            _repositoryMock.Verify(x => x.AddOrUpdatePeer(registerCommand.Peer));
        }

        [Test]
        public void should_set_registering_peer_up_and_responding()
        {
            var peerDescriptor = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://abctest:123", typeof(FakeCommand));
            peerDescriptor.Peer.IsUp = false;
            peerDescriptor.Peer.IsResponding = false;

            var registerCommand = new RegisterPeerCommand(peerDescriptor);

            _handler.Handle(registerCommand);

            _repositoryMock.Verify(x => x.AddOrUpdatePeer(It.Is<PeerDescriptor>(p => p.Peer.IsUp && p.Peer.IsResponding)));
        }

        [Test]
        public void should_reply_with_registred_peers()
        {
            var registredPeerDescriptor = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://abctest:456", typeof(FakeCommand));
            _repositoryMock.Setup(x => x.GetPeers()).Returns(new[] { registredPeerDescriptor });
            var command = new RegisterPeerCommand(TestDataBuilder.CreatePersistentPeerDescriptor("tcp://abctest:123", typeof(FakeCommand)));

            _handler.Handle(command);

            var response = (RegisterPeerResponse)_bus.LastReplyResponse;
            response.PeerDescriptors.Single().ShouldHaveSamePropertiesAs(registredPeerDescriptor);
        }

        [Test]
        public void should_publish_started_event()
        {
            var peerDescriptor = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://abctest:123", typeof(FakeCommand));
            var command = new RegisterPeerCommand(peerDescriptor);

            _handler.Handle(command);

            _bus.ExpectExactly(new PeerStarted(peerDescriptor));
        }

        [Test]
        public void should_throw_if_a_blacklisted_peer_tries_to_register()
        {
            var blacklistedPeer = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://blacklistedpeer:123", typeof(FakeCommand));
            var registerCommand = new RegisterPeerCommand(blacklistedPeer);
            _handler.Context = MessageContext.CreateTest(new OriginatorInfo(blacklistedPeer.Peer.Id, blacklistedPeer.Peer.EndPoint, "BLACKLISTEDMACHINE", "initiator"));

            var exception = typeof(InvalidOperationException).ShouldBeThrownBy(() => _handler.Handle(registerCommand));

            exception.Message.ShouldEqual("Peer BLACKLISTEDMACHINE is not allowed to register on this directory");
        }

        [Test]
        public void should_throw_if_an_existing_peer_tries_to_register()
        {
            var existingPeer = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://existingpeer:123", typeof(FakeCommand));
            _repositoryMock.Setup(x => x.Get(existingPeer.PeerId)).Returns(existingPeer);
            var newPeer = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://newpeer:123", typeof(FakeCommand));
            var command = new RegisterPeerCommand(newPeer);

            var exception = (DomainException)typeof(DomainException).ShouldBeThrownBy(() => _handler.Handle(command));
            exception.ErrorCode.ShouldEqual(DirectoryErrorCodes.PeerAlreadyExists);
        }

        [Test]
        public void should_not_throw_if_a_not_responding_peer_already_exists()
        {
            var existingPeer = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://existingpeer:123", typeof(FakeCommand));
            existingPeer.Peer.IsResponding = false;
            _repositoryMock.Setup(x => x.GetPeers()).Returns(new[] { existingPeer });
            var newPeer = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://newpeer:123", typeof(FakeCommand));
            var command = new RegisterPeerCommand(newPeer);

            _handler.Handle(command);

            _repositoryMock.Verify(x => x.AddOrUpdatePeer(newPeer));
        }

        [Test]
        public void should_not_throw_if_an_existing_peer_is_on_the_same_host()
        {
            var existingPeer = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://existingpeer:123", typeof(FakeCommand));
            existingPeer.Peer.IsResponding = false;
            _repositoryMock.Setup(x => x.GetPeers()).Returns(new[] { existingPeer });
            var newPeer = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://existingpeer:123", typeof(FakeCommand));
            var command = new RegisterPeerCommand(newPeer);

            _handler.Handle(command);

            _repositoryMock.Verify(x => x.AddOrUpdatePeer(newPeer));
        }

        [Test]
        public void should_unregister_persistent_peer_when_unregistering()
        {
            var peerDescriptor = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://abctest:123", typeof(FakeCommand));
            peerDescriptor.TimestampUtc = SystemDateTime.UtcNow.AddSeconds(-30);
            _repositoryMock.Setup(x => x.Get(peerDescriptor.Peer.Id)).Returns(peerDescriptor);

            var command = new UnregisterPeerCommand(peerDescriptor.Peer);
            _handler.Handle(command);

            _repositoryMock.Verify(x => x.AddOrUpdatePeer(It.Is<PeerDescriptor>(peer => peer.Peer.Id == peerDescriptor.Peer.Id && peer.Peer.IsUp == false && peer.TimestampUtc == command.TimestampUtc)));
        }

        [Test]
        public void should_remove_transient_peer_when_unregistering()
        {
            var peerDescriptor = TestDataBuilder.CreateTransientPeerDescriptor("tcp://abctest:123", typeof(FakeCommand));
            _repositoryMock.Setup(x => x.Get(peerDescriptor.Peer.Id)).Returns(peerDescriptor);

            _handler.Handle(new UnregisterPeerCommand(peerDescriptor.Peer));

            _repositoryMock.Verify(x => x.RemovePeer(It.Is<PeerId>(peerId => peerId == peerDescriptor.Peer.Id)));
        }

        [Test]
        public void should_publish_stopped_event_when_unregistering_a_persistent_client()
        {
            var peerDescriptor = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://abctest:123", typeof(FakeCommand));
            peerDescriptor.TimestampUtc = SystemDateTime.UtcNow.AddSeconds(-30);
            _repositoryMock.Setup(x => x.Get(peerDescriptor.Peer.Id)).Returns(peerDescriptor);

            var command = new UnregisterPeerCommand(peerDescriptor.Peer, SystemDateTime.UtcNow.AddSeconds(-2));
            _handler.Handle(command);

            _bus.ExpectExactly(new PeerStopped(peerDescriptor.Peer, command.TimestampUtc));
        }

        [Test]
        public void should_publish_decommissioned_event_when_unregistering_a_transient_client()
        {
            var peerDescriptor = TestDataBuilder.CreateTransientPeerDescriptor("tcp://abctest:123", typeof(FakeCommand));
            _repositoryMock.Setup(x => x.Get(peerDescriptor.Peer.Id)).Returns(peerDescriptor);

            _handler.Handle(new UnregisterPeerCommand(peerDescriptor.Peer));

            _bus.ExpectExactly(new PeerDecommissioned(peerDescriptor.Peer.Id));
        }

        [Test]
        public void should_publish_peer_decommissioned()
        {
            var peerId = new PeerId("Abc.Testing.0");
         
            _handler.Handle(new DecommissionPeerCommand(peerId));

            _bus.ExpectExactly(new PeerDecommissioned(peerId));
        }

        [Test]
        public void should_remove_peer_from_repository()
        {
            var peerId = new PeerId("Abc.Testing.0");
         
            _handler.Handle(new DecommissionPeerCommand(peerId));

            _repositoryMock.Verify(x => x.RemovePeer(peerId));
        }

        [Test]
        public void should_update_peer_handled_message_and_publish_event()
        {
            var originalPeerDescriptor = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://abctest:123", typeof(FakeCommand));
            _repositoryMock.Setup(x => x.Get(originalPeerDescriptor.Peer.Id)).Returns(originalPeerDescriptor);

            PeerDescriptor updatedPeerDescriptor = null;
            _repositoryMock.Setup(x => x.AddOrUpdatePeer(It.IsAny<PeerDescriptor>())).Callback<PeerDescriptor>(peer => updatedPeerDescriptor = peer);

            var newSubscriptions = new[] { new Subscription(new MessageTypeId("Another.Handled.Type")) };
            _handler.Handle(new UpdatePeerSubscriptionsCommand(originalPeerDescriptor.Peer.Id, newSubscriptions, DateTime.UtcNow));

            updatedPeerDescriptor.Subscriptions.ShouldBeEquivalentTo(newSubscriptions);

            var handledMessageUpdateds = _bus.Messages.OfType<PeerSubscriptionsUpdated>().ToList();
            handledMessageUpdateds.Count.ShouldEqual(1);
            handledMessageUpdateds.Single().PeerDescriptor.ShouldHaveSamePropertiesAs(updatedPeerDescriptor);
        }

        [Test]
        public void should_ignore_old_peer_updates()
        {
            var originalPeerDescriptor = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://abctest:123", typeof(FakeCommand));
            originalPeerDescriptor.TimestampUtc = SystemDateTime.UtcNow.AddMinutes(1);
            _repositoryMock.Setup(x => x.Get(originalPeerDescriptor.Peer.Id)).Returns(originalPeerDescriptor);

            var newSubscriptions = new[] { new Subscription(new MessageTypeId("Another.Handled.Type")) };
            _handler.Handle(new UpdatePeerSubscriptionsCommand(originalPeerDescriptor.Peer.Id, newSubscriptions, DateTime.UtcNow));

            _repositoryMock.Verify(x => x.AddOrUpdatePeer(It.IsAny<PeerDescriptor>()), Times.Never());
            _bus.ExpectNothing();
        }

        [Test]
        public void should_not_unregister_a_peer_that_started_after_timestamp()
        {
            var peerDescriptor = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://abctest:123", typeof(FakeCommand));
            _repositoryMock.Setup(x => x.Get(peerDescriptor.PeerId)).Returns(peerDescriptor);

            var command = new UnregisterPeerCommand(peerDescriptor.Peer, peerDescriptor.TimestampUtc.Value.AddSeconds(-2));
            _handler.Handle(command);

            _repositoryMock.Verify(x => x.AddOrUpdatePeer(It.IsAny<PeerDescriptor>()), Times.Never());
            _bus.ExpectNothing();
        }
    }
}