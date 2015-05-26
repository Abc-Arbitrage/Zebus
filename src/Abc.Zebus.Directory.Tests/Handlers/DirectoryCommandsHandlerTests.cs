using System;
using System.Linq;
using Abc.Zebus.Directory.Configuration;
using Abc.Zebus.Directory.Handlers;
using Abc.Zebus.Directory.Storage;
using Abc.Zebus.Routing;
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
        public void should_remove_existing_dynamic_subscriptions_on_register()
        {
            var peerDescriptor = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://abctest:123", typeof(FakeCommand));
            var registerCommand = new RegisterPeerCommand(peerDescriptor);
            
            _handler.Handle(registerCommand);

            _repositoryMock.Verify(x => x.AddOrUpdatePeer(registerCommand.Peer));
            _repositoryMock.Verify(x => x.RemoveAllDynamicSubscriptionsForPeer(registerCommand.Peer.PeerId, It.Is<DateTime>(d => d == registerCommand.Peer.TimestampUtc.Value)));
        }

        [Test]
        public void should_specify_datetime_kind_when_removing_all_subscriptions_for_a_peer_during_register()
        {
            var peerDescriptor = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://abctest:123", typeof(FakeCommand));
            peerDescriptor.TimestampUtc = new DateTime(DateTime.Now.Ticks, DateTimeKind.Unspecified);
            var registerCommand = new RegisterPeerCommand(peerDescriptor);

            _handler.Handle(registerCommand);

            _repositoryMock.Verify(x => x.AddOrUpdatePeer(registerCommand.Peer));
            _repositoryMock.Verify(x => x.RemoveAllDynamicSubscriptionsForPeer(registerCommand.Peer.PeerId, It.Is<DateTime>(d => d.Kind == DateTimeKind.Utc)));
        }

        [Test]
        public void should_reply_with_registred_peers()
        {
            var registredPeerDescriptor = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://abctest:456", typeof(FakeCommand));
            _repositoryMock.Setup(x => x.GetPeers(It.Is<bool>(loadDynamicSubs => loadDynamicSubs))).Returns(new[] { registredPeerDescriptor });
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
            _repositoryMock.Setup(x => x.GetPeers(It.IsAny<bool>())).Returns(new[] { existingPeer });
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
            _repositoryMock.Setup(x => x.GetPeers(It.IsAny<bool>())).Returns(new[] { existingPeer });
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
        public void should_update_peer_subscriptions_by_types_and_publish_event()
        {
            var peerDescriptor = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://abctest:123", typeof(FakeCommand));
            var subscriptionsForTypes = new[]
            {
                new SubscriptionsForType(MessageUtil.GetTypeId(typeof(int)), BindingKey.Empty),
                new SubscriptionsForType(MessageUtil.GetTypeId(typeof(double)), new BindingKey("bla"))
            };
            var now = DateTime.UtcNow;
            
            _handler.Handle(new UpdatePeerSubscriptionsForTypesCommand(peerDescriptor.PeerId, now, subscriptionsForTypes));

            _repositoryMock.Verify(repo => repo.AddDynamicSubscriptionsForTypes(peerDescriptor.PeerId, now, subscriptionsForTypes));
            _bus.ExpectExactly(new PeerSubscriptionsForTypesUpdated(peerDescriptor.PeerId, now, subscriptionsForTypes));
        }

        [Test]
        public void should_handle_null_timestamp_when_removing_all_subscriptions_for_a_peer()
        {
            var peerDescriptor = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://abctest:123", typeof(FakeCommand));
            peerDescriptor.TimestampUtc = null;
            var registerCommand = new RegisterPeerCommand(peerDescriptor);

            Assert.That(() => _handler.Handle(registerCommand), Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("The TimestampUtc must be provided when registering"));
        }

        [Test]
        public void should_specify_datetime_kind_when_adding_subscriptions_for_a_type()
        {
            var peerDescriptor = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://abctest:123", typeof(FakeCommand));
            var subscriptionsForTypes = new[] { new SubscriptionsForType(MessageUtil.GetTypeId(typeof(int)), BindingKey.Empty) };
            var unspecifiedNow = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            
            _handler.Handle(new UpdatePeerSubscriptionsForTypesCommand(peerDescriptor.PeerId, unspecifiedNow, subscriptionsForTypes));

            _repositoryMock.Verify(repo => repo.AddDynamicSubscriptionsForTypes(peerDescriptor.PeerId, It.Is<DateTime>(date => date.Kind == DateTimeKind.Utc), subscriptionsForTypes));
        }

        [Test]
        public void should_specify_datetime_kind_when_removing_subscriptions_for_a_type()
        {
            var peerDescriptor = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://abctest:123", typeof(FakeCommand));
            var subscriptionsForTypes = new[] { new SubscriptionsForType(MessageUtil.GetTypeId(typeof(int)), new BindingKey[0]) };
            var unspecifiedNow = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            
            _handler.Handle(new UpdatePeerSubscriptionsForTypesCommand(peerDescriptor.PeerId, unspecifiedNow, subscriptionsForTypes));

            _repositoryMock.Verify(repo => repo.RemoveDynamicSubscriptionsForTypes(peerDescriptor.PeerId, It.Is<DateTime>(date => date.Kind == DateTimeKind.Utc), new[] { MessageUtil.GetTypeId(typeof(int)) }));
        }

        [Test]
        public void should_handle_null_bindingkeys_array_when_removing_subscriptions()
        {
            var peerDescriptor = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://abctest:123", typeof(FakeCommand));
            var subscriptionsForTypes = new[] { new SubscriptionsForType(MessageUtil.GetTypeId(typeof(int)), null) };
            var now = DateTime.UtcNow;
            
            _handler.Handle(new UpdatePeerSubscriptionsForTypesCommand(peerDescriptor.PeerId, now, subscriptionsForTypes));

            _repositoryMock.Verify(repo => repo.RemoveDynamicSubscriptionsForTypes(peerDescriptor.PeerId, now, new[] { MessageUtil.GetTypeId(typeof(int)) }));
        }

        [Test]
        public void should_handle_null_subscriptionsByType_array()
        {
            var peerDescriptor = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://abctest:123", typeof(FakeCommand));
            var now = DateTime.UtcNow;

            Assert.That(() => _handler.Handle(new UpdatePeerSubscriptionsForTypesCommand(peerDescriptor.PeerId, now, null)),
                        Throws.Nothing);

            _bus.ExpectNothing();
        }

        [Test]
        public void should_remove_peer_subscriptions_for_a_type_if_there_are_no_binding_keys()
        {
            var now = DateTime.UtcNow;
            var peerDescriptor = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://abctest:123", typeof(FakeCommand));
            SubscriptionsForType[] addedSubscriptions = null;
            MessageTypeId[] removedMessageTypeIds = null;
            _repositoryMock.Setup(repo => repo.AddDynamicSubscriptionsForTypes(peerDescriptor.PeerId, now, It.IsAny<SubscriptionsForType[]>()))
                           .Callback((PeerId peerId, DateTime timestampUtc, SubscriptionsForType[] subs) => addedSubscriptions = subs);
            _repositoryMock.Setup(repo => repo.RemoveDynamicSubscriptionsForTypes(peerDescriptor.PeerId, now, It.IsAny<MessageTypeId[]>()))
                           .Callback((PeerId peerId, DateTime timestampUtc, MessageTypeId[] ids) => removedMessageTypeIds = ids);
            var subscriptionsForTypes = new[]
            {
                new SubscriptionsForType(MessageUtil.GetTypeId(typeof(int))),
                new SubscriptionsForType(MessageUtil.GetTypeId(typeof(double)), BindingKey.Empty)
            };
            
            _handler.Handle(new UpdatePeerSubscriptionsForTypesCommand(peerDescriptor.PeerId, now, subscriptionsForTypes));

            var addedSubscription = addedSubscriptions.ExpectedSingle();
            addedSubscription.ShouldHaveSamePropertiesAs(new SubscriptionsForType(MessageUtil.GetTypeId(typeof(double)), BindingKey.Empty));
            var removedMessageTypeId = removedMessageTypeIds.ExpectedSingle();
            removedMessageTypeId.ShouldHaveSamePropertiesAs(MessageUtil.GetTypeId(typeof(int)));
            _bus.ExpectExactly(new PeerSubscriptionsForTypesUpdated(peerDescriptor.PeerId, now, subscriptionsForTypes));
        }

        [Test]
        public void should_throw_an_explicit_exception_when_updating_the_subscriptions_of_a_decommissioned_peer()
        {
            Assert.That(() => _handler.Handle(new UpdatePeerSubscriptionsCommand(new PeerId("Abc.NonExistingPeer.0"), new Subscription[0], DateTime.UtcNow)),
                        Throws.InstanceOf<InvalidOperationException>().With.Property("Message").EqualTo("The specified Peer (Abc.NonExistingPeer.0) does not exist."));
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