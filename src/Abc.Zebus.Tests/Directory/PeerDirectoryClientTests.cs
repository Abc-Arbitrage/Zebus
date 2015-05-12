using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Directory;
using Abc.Zebus.Routing;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Extensions;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Directory
{
    // WARN: in the tests, do not reuse the same PeerDescriptor instance multiple times when calling PeerDirectoryClient methods,
    //       you might end up with a test passing by coincidence

    [TestFixture]
    public partial class PeerDirectoryClientTests
    {
        private PeerDirectoryClient _directory;
        private Mock<IBusConfiguration> _configurationMock;
        private TestBus _bus;
        private Peer _self;
        private Peer _otherPeer;

        [SetUp]
        public void Setup()
        {
            _configurationMock = new Mock<IBusConfiguration>();
            _configurationMock.SetupGet(x => x.DirectoryServiceEndPoints).Returns(new[] { "tcp://main-directory:777", "tcp://backup-directory:777" });
            _configurationMock.SetupGet(x => x.RegistrationTimeout).Returns(100.Milliseconds());
            _configurationMock.SetupGet(x => x.IsDirectoryPickedRandomly).Returns(false);

            _directory = new PeerDirectoryClient(_configurationMock.Object);
            _bus = new TestBus();

            _self = new Peer(new PeerId("Abc.Testing.0"), "tcp://abctest:123");
            _otherPeer = new Peer(new PeerId("Abc.Testing.1"), "tcp://abctest:789");
        }

        [Test]
        public void should_register_peer([Values(true, false)]bool isPersistent)
        {
            var subscriptions = TestDataBuilder.CreateSubscriptions<FakeCommand>();
            _configurationMock.SetupGet(x => x.IsPersistent).Returns(isPersistent);
            _bus.AddHandler<RegisterPeerCommand>(x => new RegisterPeerResponse(new PeerDescriptor[0]));

            using (SystemDateTime.PauseTime())
            {
                _directory.Register(_bus, _self, subscriptions);

                var expectedRecipientId = new PeerId("Abc.Zebus.DirectoryService.0");
                _bus.Commands.Count().ShouldEqual(1);

                var register = _bus.Commands.OfType<RegisterPeerCommand>().ExpectedSingle();
                register.Peer.PeerId.ShouldEqual(_self.Id);
                register.Peer.IsPersistent.ShouldEqual(isPersistent);
                register.Peer.TimestampUtc.Value.ShouldBeGreaterOrEqualThan(SystemDateTime.UtcNow);
                register.Peer.Subscriptions.ShouldBeEquivalentTo(subscriptions);

                _bus.GetRecipientPeer(register).Id.ShouldEqual(expectedRecipientId);
            }
        }

        [Test]
        public void should_re_register_peer()
        {
            var peers = new List<PeerDescriptor>();
            _bus.AddHandler<RegisterPeerCommand>(x => new RegisterPeerResponse(peers.ToArray()));

            var peer1 = new Peer(new PeerId("Abc.X.1"), "tcp://x:1");
            peers.Add(peer1.ToPeerDescriptor(true, typeof(FakeCommand)));

            _directory.Register(_bus, _self, new Subscription[0]);
            _directory.GetPeersHandlingMessage(new FakeCommand(0)).ExpectedSingle().Id.ShouldEqual(peer1.Id);

            peers.Clear();
            var peer2 = new Peer(new PeerId("Abc.X.2"), "tcp://x:2");
            peers.Add(peer2.ToPeerDescriptor(true, typeof(FakeCommand)));

            _directory.Register(_bus, _self, new Subscription[0]);
            _directory.GetPeersHandlingMessage(new FakeCommand(0)).ExpectedSingle().Id.ShouldEqual(peer2.Id);
        }

        [Test]
        public void should_not_register_existing_peer()
        {
            var subscriptions = TestDataBuilder.CreateSubscriptions<FakeCommand>();
            _configurationMock.SetupGet(x => x.IsPersistent).Returns(true);
            _bus.AddHandler<RegisterPeerCommand>(x =>
            {
                throw new DomainException(DirectoryErrorCodes.PeerAlreadyExists, "Peer already exists");
            } );

            using (SystemDateTime.PauseTime())
            {
                Assert.Throws<TimeoutException>(() => _directory.Register(_bus, _self, subscriptions));
            }
        }

        [Test]
        public void should_update_peer()
        {
            _bus.AddHandler<RegisterPeerCommand>(x => new RegisterPeerResponse(new PeerDescriptor[0]));
            _directory.Register(_bus, _self, new Subscription[0]);

            var expectedSubscriptions = new[] { SubscriptionsForType.Create<FakeCommand>(new BindingKey("plip"), new BindingKey("plop")) };
            _directory.UpdateSubscriptions(_bus, expectedSubscriptions);

            var command = _bus.Commands.OfType<UpdatePeerSubscriptionsForTypesCommand>().ExpectedSingle();
            command.PeerId.ShouldEqual(_self.Id);
            command.SubscriptionsForTypes.ShouldEqual(expectedSubscriptions);
        }

        [Test]
        public void should_not_send_updatesubscriptions_when_not_needed()
        {
            _bus.AddHandler<RegisterPeerCommand>(x => new RegisterPeerResponse(new PeerDescriptor[0]));
            _directory.Register(_bus, _self, new Subscription[0]);

            _directory.UpdateSubscriptions(_bus, new SubscriptionsForType[0]);

            _bus.Commands.OfType<RegisterPeerCommand>().ExpectedSingle();
            _bus.Commands.OfType<UpdatePeerSubscriptionsForTypesCommand>().ShouldBeEmpty();
        }
        
        [Test]
        public void should_timestamp_updatesubscriptions_with_a_minimum_ten_ticks_granularity()
        {
            _bus.AddHandler<RegisterPeerCommand>(x => new RegisterPeerResponse(new PeerDescriptor[0]));
            _directory.Register(_bus, _self, new Subscription[0]);

            var subscriptions = new[] { SubscriptionsForType.Create<FakeCommand>(BindingKey.Empty) };
            for (var i = 0; i < 100; ++i)
                _directory.UpdateSubscriptions(_bus, subscriptions);

            var commands = _bus.Commands.OfType<UpdatePeerSubscriptionsForTypesCommand>().ToList();
            commands.ShouldNotBeEmpty();
            var lastTimestamp = DateTime.MinValue;
            foreach (var updatePeerSubscriptionsCommand in commands)
            {
                var timeSinceLastCommand = updatePeerSubscriptionsCommand.TimestampUtc - lastTimestamp;
                timeSinceLastCommand.ShouldBeGreaterOrEqualThan(10.Ticks());
                lastTimestamp = updatePeerSubscriptionsCommand.TimestampUtc;
            }
        }

        [Test]
        public void should_have_unique_timestamp_in_UpdatePeerSubscriptionsCommand()
        {
            _bus.AddHandler<RegisterPeerCommand>(x => new RegisterPeerResponse(new PeerDescriptor[0]));
            _directory.Register(_bus, _self, new Subscription[0]);

            var lastTimestamp = DateTime.MinValue;
            for (int i = 0; i < 100; i++)
            {
                var subscriptions = new[] { SubscriptionsForType.Create<FakeCommand>(new BindingKey(i.ToString())) };
                _directory.UpdateSubscriptions(_bus, subscriptions);

                var command = _bus.Commands.OfType<UpdatePeerSubscriptionsForTypesCommand>().ExpectedSingle();
                command.TimestampUtc.ShouldBeGreaterThan(lastTimestamp);

                _bus.ClearMessages();
                lastTimestamp = command.TimestampUtc;
            }
        }

        [Test]
        public void should_update_started_peer_with_no_timestamp_on_PeerSubscriptionsUpdated()
        {
            var descriptor1 = _otherPeer.ToPeerDescriptor(true);
            descriptor1.TimestampUtc = default(DateTime);
            _directory.Handle(new PeerStarted(descriptor1));

            var descriptor2 = _otherPeer.ToPeerDescriptor(true, typeof(FakeCommand));
            descriptor2.TimestampUtc = default(DateTime);
            _directory.Handle(new PeerSubscriptionsUpdated(descriptor2));

            var targetPeer = _directory.GetPeersHandlingMessage(new FakeCommand(0)).ExpectedSingle();
            targetPeer.Id.ShouldEqual(_otherPeer.Id);
        }

        [Test]
        public void should_discard_outdated_updates_on_PeerSubscriptionsUpdated()
        {
            int updates = 0;
            _directory.PeerUpdated += (id, action) => updates++;

            var descriptor1 = _otherPeer.ToPeerDescriptor(true);
            descriptor1.TimestampUtc = DateTime.UtcNow;
            _directory.Handle(new PeerStarted(descriptor1));

            var descriptor2 = _otherPeer.ToPeerDescriptor(true, typeof(FakeCommand));
            descriptor2.TimestampUtc = DateTime.UtcNow;
            _directory.Handle(new PeerSubscriptionsUpdated(descriptor2));
            
            descriptor2.TimestampUtc = default(DateTime);
            _directory.Handle(new PeerSubscriptionsUpdated(descriptor2));

            _directory.GetPeersHandlingMessage(new FakeCommand(0)).ExpectedSingle();

            updates.ShouldEqual(2);
        }

        [Test]
        public void should_discard_outdated_updates_on_PeerSubscriptionsForTypesUpdated()
        {
            int updates = 0;
            _directory.PeerUpdated += (id, action) => updates++;

            var peerDescriptor = _otherPeer.ToPeerDescriptor(true);
            peerDescriptor.TimestampUtc = DateTime.UtcNow;
            _directory.Handle(new PeerStarted(peerDescriptor));

            _directory.Handle(new PeerSubscriptionsForTypesUpdated(peerDescriptor.PeerId, DateTime.UtcNow, MessageUtil.TypeId<FakeCommand>(), BindingKey.Empty));
            _directory.Handle(new PeerSubscriptionsForTypesUpdated(peerDescriptor.PeerId, default(DateTime), MessageUtil.TypeId<FakeCommand>(), BindingKey.Empty));

            _directory.GetPeersHandlingMessage(new FakeCommand(0)).ExpectedSingle();

            updates.ShouldEqual(2);
        }

        [Test]
        public void should_update_started_peer_with_no_timestamp_on_PeerSubscriptionsForTypesUpdated()
        {
            var peerDescriptor = _otherPeer.ToPeerDescriptor(true);
            peerDescriptor.TimestampUtc = default(DateTime);
            _directory.Handle(new PeerStarted(peerDescriptor));

            _directory.Handle(new PeerSubscriptionsForTypesUpdated(peerDescriptor.PeerId, default(DateTime), MessageUtil.TypeId<FakeCommand>(), BindingKey.Empty));

            var targetPeer = _directory.GetPeersHandlingMessage(new FakeCommand(0)).ExpectedSingle();
            targetPeer.Id.ShouldEqual(_otherPeer.Id);
        }


        [Test]
        public void should_remove_subscriptions_to_type_when_no_binding_keys_are_provided()
        {
            var peerDescriptor = _otherPeer.ToPeerDescriptor(true, typeof(FakeCommand));
            peerDescriptor.TimestampUtc = default(DateTime);
            _directory.Handle(new PeerStarted(peerDescriptor));

            _directory.Handle(new PeerSubscriptionsForTypesUpdated(peerDescriptor.PeerId, default(DateTime), MessageUtil.TypeId<FakeCommand>()));

            _directory.GetPeersHandlingMessage(new FakeCommand(0)).ShouldBeEmpty();
        }

        [Test]
        public void should_get_self_handled_messages()
        {
            _bus.AddHandler<RegisterPeerCommand>(x => new RegisterPeerResponse(new PeerDescriptor[0]));

            var subscriptions = TestDataBuilder.CreateSubscriptions<FakeCommand>();
            _directory.Register(_bus, _self, subscriptions);

            var peerHandlingMessage = _directory.GetPeersHandlingMessage(new FakeCommand(0)).Single();
            peerHandlingMessage.Id.ShouldEqual(_self.Id);
        }

        [Test]
        public void should_get_peer_with_matching_subscrition_binding_key()
        {
            _bus.AddHandler<RegisterPeerCommand>(x => new RegisterPeerResponse(new PeerDescriptor[0]));

            var command = new FakeRoutableCommand(10, "name");
            var subscriptions = new[] { new Subscription(command.TypeId(), new BindingKey("10", "#")) };
            _directory.Register(_bus, _self, subscriptions);

            var peerHandlingMessage = _directory.GetPeersHandlingMessage(command).Single();
            peerHandlingMessage.Id.ShouldEqual(_self.Id);
        }

        [Test]
        public void should_not_get_peer_with_non_matching_subscrition_binding_key()
        {
            _bus.AddHandler<RegisterPeerCommand>(x => new RegisterPeerResponse(new PeerDescriptor[0]));

            var command = new FakeRoutableCommand(10, "name");
            var subscriptions = new[] { new Subscription(command.TypeId(), new BindingKey("5", "#")) };
            _directory.Register(_bus, _self, subscriptions);

            _directory.GetPeersHandlingMessage(command).ShouldBeEmpty();
        }

        [Test]
        public void should_load_descriptors_after_register()
        {
            var clientPeerDescriptor = _otherPeer.ToPeerDescriptor(true, typeof(FakeEvent));
            _bus.AddHandler<RegisterPeerCommand>(x => new RegisterPeerResponse(new[] { clientPeerDescriptor }));

            _directory.Register(_bus, _self, new Subscription[0]);

            var peerHandlingMessage = _directory.GetPeersHandlingMessage(new FakeEvent(0)).Single();
            peerHandlingMessage.Id.ShouldEqual(clientPeerDescriptor.Peer.Id);
            peerHandlingMessage.EndPoint.ShouldEqual(clientPeerDescriptor.Peer.EndPoint);
        }

        [Test]
        public void should_get_peer_by_id()
        {
            var clientPeerDescriptor = _otherPeer.ToPeerDescriptor(true);
            var peerStarted = new PeerStarted(clientPeerDescriptor);
            _directory.Handle(peerStarted);

            var peerDescriptor = _directory.GetPeerDescriptor(clientPeerDescriptor.Peer.Id);

            peerDescriptor.ShouldHaveSamePropertiesAs(clientPeerDescriptor);
        }

        [Test]
        public void should_unregister_peer()
        {
            _bus.AddHandler<RegisterPeerCommand>(x => new RegisterPeerResponse(new PeerDescriptor[0]));

            _directory.Register(_bus, _self, new Subscription[0]);

            using (SystemDateTime.PauseTime())
            {
                _directory.Unregister(_bus);
                _directory.Unregister(_bus);

                var unregisterPeerCommands = _bus.Commands.OfType<UnregisterPeerCommand>().ToList();
                unregisterPeerCommands.Count.ShouldEqual(2);
                unregisterPeerCommands[0].PeerId.ShouldEqual(_self.Id);
                unregisterPeerCommands[1].PeerId.ShouldEqual(_self.Id);
                unregisterPeerCommands[1].TimestampUtc.Value.ShouldBeGreaterThan(unregisterPeerCommands[0].TimestampUtc.Value);
            }
        }

        [Test]
        public void unregister_should_be_blocking()
        {
            _bus.AddHandler<RegisterPeerCommand>(x => new RegisterPeerResponse(new PeerDescriptor[0]));
            _directory.Register(_bus, _self, new Subscription[0]);

            var startUnregisterSignal = new AutoResetEvent(false);
            var stopUnregisterSignal = new AutoResetEvent(false);

            _bus.HandlerExecutor = new TestBus.AsyncHandlerExecutor();
            _bus.AddHandler<UnregisterPeerCommand>(x =>
            {
                startUnregisterSignal.Set();
                stopUnregisterSignal.WaitOne();
            });

            var unregistration = Task.Factory.StartNew(() => _directory.Unregister(_bus));

            var started = startUnregisterSignal.WaitOne(2.Seconds());
            started.ShouldBeTrue();

            unregistration.IsCompleted.ShouldBeFalse();

            stopUnregisterSignal.Set();

            unregistration.Wait(300);
            unregistration.IsCompleted.ShouldBeTrue();
        }

        [Test]
        public void should_add_started_peers()
        {
            var updatedPeerId = default(PeerId);
            var updateAction = PeerUpdateAction.Decommissioned;
            _directory.PeerUpdated += (id, action) =>
                {
                    updatedPeerId = id;
                    updateAction = action;
                };
            var clientPeerDescriptor = _otherPeer.ToPeerDescriptor(true, typeof(FakeEvent));
            var peerStarted = new PeerStarted(clientPeerDescriptor);

            _directory.Handle(peerStarted);

            var peerHandlingMessage = _directory.GetPeersHandlingMessage(new FakeEvent(0)).Single();
            peerHandlingMessage.Id.ShouldEqual(clientPeerDescriptor.Peer.Id);
            peerHandlingMessage.EndPoint.ShouldEqual(clientPeerDescriptor.Peer.EndPoint);
            peerHandlingMessage.IsUp.ShouldBeTrue();
            updatedPeerId.ShouldEqual(_otherPeer.Id);
            updateAction.ShouldEqual(PeerUpdateAction.Started);
        }

        [Test]
        public void should_update_stopped_peers()
        {
            var updatedPeerId = default(PeerId);
            var updateAction = PeerUpdateAction.Decommissioned;
            _directory.PeerUpdated += (id, action) =>
            {
                updatedPeerId = id;
                updateAction = action;
            };

            _directory.Handle(new PeerStarted(_otherPeer.ToPeerDescriptor(true, typeof(FakeEvent))));
            _directory.Handle(new PeerStopped(_otherPeer));

            var peerHandlingMessage = _directory.GetPeersHandlingMessage(new FakeEvent(0)).Single();
            peerHandlingMessage.IsUp.ShouldBeFalse();
            updatedPeerId.ShouldEqual(_otherPeer.Id);
            updateAction.ShouldEqual(PeerUpdateAction.Stopped);
        }

        [Test]
        public void should_update_persistence_state_on_restart()
        {
            _directory.Handle(new PeerStarted(_otherPeer.ToPeerDescriptor(true)));
            _directory.Handle(new PeerStopped(_otherPeer));
            _directory.Handle(new PeerStarted(_otherPeer.ToPeerDescriptor(false)));

            var peerDescriptor = _directory.GetPeerDescriptor(_otherPeer.Id);
            peerDescriptor.IsPersistent.ShouldBeFalse();
        }

        [Test]
        public void should_update_peers()
        {
            _directory.Handle(new PeerStarted(_otherPeer.ToPeerDescriptor(true, typeof(FakeEvent), typeof(OtherFakeEvent1))));
            _directory.Handle(new PeerStopped(_otherPeer));

            const string newEndPoint = "tcp://new-end-point:111";
            var newPeer = new Peer(_otherPeer.Id, newEndPoint);
            _directory.Handle(new PeerStarted(newPeer.ToPeerDescriptor(true, typeof(FakeCommand), typeof(OtherFakeEvent1))));

            _directory.GetPeersHandlingMessage(new FakeEvent(0)).ShouldBeEmpty();
            var peer = _directory.GetPeersHandlingMessage(new FakeCommand(0)).Single();
            peer.Id.ShouldEqual(_otherPeer.Id);
            peer.EndPoint.ShouldEqual(newEndPoint);

            var peer2 = _directory.GetPeersHandlingMessage(new OtherFakeEvent1()).Single();
            peer2.Id.ShouldEqual(_otherPeer.Id);
            peer2.EndPoint.ShouldEqual(newEndPoint);
        }

        [Test]
        public void should_connect_to_next_directory_if_first_is_failing()
        {
            _bus.HandlerExecutor = new TestBus.AsyncHandlerExecutor();
            _bus.AddHandlerForPeer<RegisterPeerCommand>(new PeerId("Abc.Zebus.DirectoryService.0"), x => { Thread.Sleep(1.Second()); return new RegisterPeerResponse(new PeerDescriptor[0]); });
            _bus.AddHandlerForPeer<RegisterPeerCommand>(new PeerId("Abc.Zebus.DirectoryService.1"), x => new RegisterPeerResponse(new PeerDescriptor[0]));

            var subscriptions = TestDataBuilder.CreateSubscriptions<FakeCommand>();
            _directory.Register(_bus, _self, subscriptions);

            var contactedPeers = _bus.GetContactedPeerIds().ToList();
            contactedPeers.Count.ShouldEqual(2);
            contactedPeers.ShouldContain(new PeerId("Abc.Zebus.DirectoryService.0"));
            contactedPeers.ShouldContain(new PeerId("Abc.Zebus.DirectoryService.1"));
        }

        [Test]
        public void should_order_directory_peers_randomly()
        {
            _configurationMock.SetupGet(x => x.IsDirectoryPickedRandomly).Returns(true);

            for (var i = 0; i < 100; i++)
            {
                var directoryPeers = _directory.GetDirectoryPeers();
                if (directoryPeers.First().EndPoint == "tcp://backup-directory:777")
                    return;
                Thread.Sleep(1); // Ensures that the underlying Random changes seed between tries
            }
            Assert.Fail("100 tries didn't succeed in returning a shuffled version of the directory peers");
        }

        [Test]
        public void should_remove_peer_from_cache_when_decommission()
        {
            var updatedPeerId = default(PeerId);
            var updateAction = PeerUpdateAction.Started;
            _directory.PeerUpdated += (id, action) =>
            {
                updatedPeerId = id;
                updateAction = action;
            };

            _directory.Handle(new PeerStarted(_otherPeer.ToPeerDescriptor(true, typeof(FakeCommand))));
            _directory.Handle(new PeerDecommissioned(_otherPeer.Id));

            var peersHandlingFakeCommand = _directory.GetPeersHandlingMessage(new FakeCommand(0));
            peersHandlingFakeCommand.ShouldBeEmpty();
            _directory.GetPeerDescriptor(_otherPeer.Id).ShouldBeNull();
            updatedPeerId.ShouldEqual(_otherPeer.Id);
            updateAction.ShouldEqual(PeerUpdateAction.Decommissioned);
        }

        [Test]
        public void should_get_all_peer_descriptor()
        {
            var clientPeerDescriptor = _otherPeer.ToPeerDescriptor(true, typeof(FakeEvent));
            var peerStarted = new PeerStarted(clientPeerDescriptor);

            _directory.Handle(peerStarted);

            _directory.GetPeerDescriptors().Count().ShouldEqual(1);
        }

        [Test]
        public void should_implement_PingPeerCommand_Handler()
        {
            typeof(PeerDirectoryClient).Is<IMessageHandler<PingPeerCommand>>().ShouldBeTrue();
        }

        [Test]
        public void should_update_peer_handled_message_on_PeerSubscriptionsUpdated()
        {
            var updatedPeerId = default(PeerId);
            var updateAction = PeerUpdateAction.Decommissioned;
            _directory.PeerUpdated += (id, action) =>
            {
                updatedPeerId = id;
                updateAction = action;
            };

            _directory.Handle(new PeerStarted(_otherPeer.ToPeerDescriptor(true, typeof(FakeEvent))));
            _directory.Handle(new PeerSubscriptionsUpdated(_otherPeer.ToPeerDescriptor(true, typeof(FakeEvent), typeof(FakeCommand))));

            _directory.GetPeersHandlingMessage(new FakeEvent(0)).ShouldNotBeEmpty();
            var peer = _directory.GetPeersHandlingMessage(new FakeCommand(0)).Single();
            peer.Id.ShouldEqual(_otherPeer.Id);
            updatedPeerId.ShouldEqual(_otherPeer.Id);
            updateAction.ShouldEqual(PeerUpdateAction.Updated);

            _directory.Handle(new PeerSubscriptionsUpdated(_otherPeer.ToPeerDescriptor(true, typeof(FakeEvent))));
            _directory.GetPeersHandlingMessage(new FakeEvent(0)).ShouldNotBeEmpty();
            _directory.GetPeersHandlingMessage(new FakeCommand(0)).ShouldBeEmpty();
        }

        [Test]
        public void should_update_peer_handled_message_on_PeerSubscriptionsForTypesUpdated()
        {
            var updatedPeerId = default(PeerId);
            var updateAction = PeerUpdateAction.Decommissioned;
            _directory.PeerUpdated += (id, action) =>
            {
                updatedPeerId = id;
                updateAction = action;
            };

            _directory.Handle(new PeerStarted(_otherPeer.ToPeerDescriptor(true, typeof(FakeEvent))));

            _directory.GetPeersHandlingMessage(new FakeEvent(0)).ShouldNotBeEmpty();
            _directory.GetPeersHandlingMessage(new FakeCommand(0)).ShouldBeEmpty();

            _directory.Handle(new PeerSubscriptionsForTypesUpdated(_otherPeer.Id, DateTime.UtcNow, MessageUtil.TypeId<FakeCommand>(), BindingKey.Empty));

            updatedPeerId.ShouldEqual(_otherPeer.Id);
            updateAction.ShouldEqual(PeerUpdateAction.Updated);

            _directory.GetPeersHandlingMessage(new FakeEvent(0)).ShouldNotBeEmpty();
            _directory.GetPeersHandlingMessage(new FakeCommand(0)).ShouldNotBeEmpty();
        }

        [Test]
        public void should_ignore_old_subscription_update_on_PeerSubscriptionsUpdated()
        {
            _directory.Handle(new PeerStarted(_otherPeer.ToPeerDescriptor(true)));

            var peerDescriptorWithNewSubscription = _otherPeer.ToPeerDescriptor(true, typeof(FakeCommand));
            peerDescriptorWithNewSubscription.TimestampUtc = SystemDateTime.UtcNow.AddMinutes(1);
            _directory.Handle(new PeerSubscriptionsUpdated(peerDescriptorWithNewSubscription));

            var outdatedPeerDescriptor = _otherPeer.ToPeerDescriptor(true);
            outdatedPeerDescriptor.TimestampUtc = SystemDateTime.UtcNow;
            _directory.Handle(new PeerSubscriptionsUpdated(outdatedPeerDescriptor));

            _directory.GetPeersHandlingMessage(new FakeCommand(0)).ShouldNotBeEmpty();
        }
        
        
        [Test]
        public void should_ignore_old_subscription_update_on_PeerSubscriptionsForTypesUpdated()
        {
            _directory.Handle(new PeerStarted(_otherPeer.ToPeerDescriptor(true)));
            _directory.Handle(new PeerSubscriptionsForTypesUpdated(_otherPeer.Id, SystemDateTime.UtcNow.AddMinutes(1), MessageUtil.TypeId<FakeCommand>(), BindingKey.Empty));
            _directory.Handle(new PeerSubscriptionsForTypesUpdated(_otherPeer.Id, SystemDateTime.UtcNow, MessageUtil.TypeId<FakeCommand>(), BindingKey.Empty));

            _directory.GetPeersHandlingMessage(new FakeCommand(0)).ShouldNotBeEmpty();
        }

        [Test]
        public void should_consider_timestamp_at_message_type_level()
        {
            _directory.Handle(new PeerStarted(_otherPeer.ToPeerDescriptor(true)));
            _directory.Handle(new PeerSubscriptionsForTypesUpdated(_otherPeer.Id, SystemDateTime.UtcNow.AddSeconds(20), MessageUtil.TypeId<FakeCommand>(), BindingKey.Empty));
            _directory.Handle(new PeerSubscriptionsForTypesUpdated(_otherPeer.Id, SystemDateTime.UtcNow.AddSeconds(10), MessageUtil.TypeId<FakeEvent>(), BindingKey.Empty));

            _directory.GetPeersHandlingMessage(new FakeEvent(0)).ShouldNotBeEmpty();
        }

        [Test]
        public void should_support_multiple_peers_for_same_message()
        {
            var otherPeer2 = new Peer(new PeerId("Abc.Testing.2"), "tcp://abctest:987");

            _directory.Handle(new PeerStarted(_otherPeer.ToPeerDescriptor(true, typeof(FakeEvent))));
            _directory.Handle(new PeerStarted(otherPeer2.ToPeerDescriptor(true, typeof(FakeEvent))));

            var peers = _directory.GetPeersHandlingMessage(new FakeEvent(0));
            peers.Count.ShouldEqual(2);
        }

        [Test]
        public void should_not_impact_other_peers_when_decommissioning_a_peer()
        {
            var anotherPeer = new Peer(new PeerId("Abc.Testing.2"), "tcp://abctest:987");
            _directory.Handle(new PeerStarted(_otherPeer.ToPeerDescriptor(true, typeof(FakeEvent))));
            _directory.Handle(new PeerStarted(anotherPeer.ToPeerDescriptor(true, typeof(FakeEvent))));
            
            _directory.Handle(new PeerDecommissioned(anotherPeer.Id));

            var peers = _directory.GetPeersHandlingMessage(new FakeEvent(0));
            peers.Count.ShouldEqual(1);
        }

        [Test]
        public void should_ignore_old_stop_message()
        {
            _directory.Handle(new PeerStarted(_otherPeer.ToPeerDescriptor(true, typeof(FakeEvent))));
            _directory.Handle(new PeerStopped(_otherPeer, SystemDateTime.UtcNow.AddSeconds(-5)));

            _directory.GetPeerDescriptor(_otherPeer.Id).Peer.IsUp.ShouldBeTrue();
        }

        [Test]
        public void should_not_return_duplicates_in_peers_handling_messages_after_PeerSubscriptionsUpdated()
        {
            var descriptor = _otherPeer.ToPeerDescriptor(true, new[]
            {
                Subscription.Any<FakeRoutableCommand>(),
                Subscription.Any<FakeRoutableCommand>(),
            });
            _directory.Handle(new PeerStarted(descriptor));

            var peers = _directory.GetPeersHandlingMessage(new FakeRoutableCommand(42, "Foo"));
            peers.Count.ShouldEqual(1);
            peers[0].Id.ShouldEqual(_otherPeer.Id);

            var updatedDescriptor = _otherPeer.ToPeerDescriptor(true, new[]
            {
                Subscription.Any<FakeRoutableCommand>(),
                Subscription.Matching<FakeRoutableCommand>(x => x.Id == 42),
            });
            _directory.Handle(new PeerSubscriptionsUpdated(updatedDescriptor));

            peers = _directory.GetPeersHandlingMessage(new FakeRoutableCommand(42, "Foo"));
            peers.Count.ShouldEqual(1);
            peers[0].Id.ShouldEqual(_otherPeer.Id);
        }

        [Test]
        public void should_not_return_duplicates_in_peers_handling_messages_after_PeerSubscriptionsForTypesUpdated()
        {
            var descriptor = _otherPeer.ToPeerDescriptor(true, new[]
            {
                Subscription.Any<FakeRoutableCommand>(),
                Subscription.Any<FakeRoutableCommand>(),
            });
            _directory.Handle(new PeerStarted(descriptor));

            var peers = _directory.GetPeersHandlingMessage(new FakeRoutableCommand(42, "Foo"));
            peers.Count.ShouldEqual(1);
            peers[0].Id.ShouldEqual(_otherPeer.Id);

            var subscriptionsForTypes = new []
            {
                new SubscriptionsForType(MessageUtil.TypeId<FakeRoutableCommand>(), BindingKey.Empty ),
                new SubscriptionsForType(MessageUtil.TypeId<FakeRoutableCommand>(), Subscription.Matching<FakeRoutableCommand>(x => x.Id == 42).BindingKey),
            };

            _directory.Handle(new PeerSubscriptionsForTypesUpdated(_otherPeer.Id, DateTime.UtcNow, subscriptionsForTypes));

            peers = _directory.GetPeersHandlingMessage(new FakeRoutableCommand(42, "Foo"));

            peers.Count.ShouldEqual(1);
            peers[0].Id.ShouldEqual(_otherPeer.Id);
        }

        [Test]
        public void should_handle_not_responding_peer()
        {
            _directory.Handle(new PeerStarted(_otherPeer.ToPeerDescriptor(true, typeof(FakeEvent), typeof(OtherFakeEvent1))));

            var peer = _directory.GetPeersHandlingMessage(new FakeEvent(0)).Single();
            peer.IsUp.ShouldBeTrue();
            peer.IsResponding.ShouldBeTrue();

            _directory.Handle(new PeerNotResponding(_otherPeer.Id));
            peer.IsUp.ShouldBeTrue();
            peer.IsResponding.ShouldBeFalse();

            _directory.Handle(new PeerResponding(_otherPeer.Id));
            peer.IsUp.ShouldBeTrue();
            peer.IsResponding.ShouldBeTrue();

            _directory.Handle(new PeerStopped(_otherPeer.Id, _otherPeer.EndPoint));
            peer.IsUp.ShouldBeFalse();
            peer.IsResponding.ShouldBeFalse();
        }

        [Test]
        public void should_have_unique_peer_instance_for_all_messages()
        {
            _directory.Handle(new PeerStarted(_otherPeer.ToPeerDescriptor(true, typeof(FakeEvent))));
            _directory.Handle(new PeerStopped(_otherPeer));

            var otherPeerWithNewEndPoint1 = new Peer(_otherPeer.Id, "tcp://wtf:1");
            _directory.Handle(new PeerStarted(otherPeerWithNewEndPoint1.ToPeerDescriptor(true, typeof(FakeEvent), typeof(OtherFakeEvent1))));
            _directory.Handle(new PeerStopped(otherPeerWithNewEndPoint1));

            var otherPeerWithNewEndPoint2 = new Peer(_otherPeer.Id, "tcp://wtf:2");
            _directory.Handle(new PeerStarted(otherPeerWithNewEndPoint2.ToPeerDescriptor(true, typeof(FakeEvent), typeof(OtherFakeEvent1), typeof(OtherFakeEvent2))));

            var peer1 = _directory.GetPeersHandlingMessage(new FakeEvent(0)).ExpectedSingle();
            var peer2 = _directory.GetPeersHandlingMessage(new OtherFakeEvent1()).ExpectedSingle();
            var peer3 = _directory.GetPeersHandlingMessage(new OtherFakeEvent2()).ExpectedSingle();

            peer1.EndPoint.ShouldEqual(peer2.EndPoint);
            peer1.EndPoint.ShouldEqual(peer3.EndPoint);
        }

        [Test]
        public void should_handle_update_subscriptions_for_type()
        {
            _directory.Handle(new PeerStarted(_otherPeer.ToPeerDescriptor(true, typeof(FakeEvent))));
            _directory.Handle(new PeerSubscriptionsForTypesUpdated(_otherPeer.Id, DateTime.UtcNow.AddTicks(1), new MessageTypeId(typeof(OtherFakeEvent3)), new BindingKey("3")));

            _directory.GetPeerDescriptor(_otherPeer.Id).Subscriptions.Length.ShouldEqual(2);
            _directory.GetPeersHandlingMessage(new FakeEvent(0)).ExpectedSingle().Id.ShouldEqual(_otherPeer.Id);
            _directory.GetPeersHandlingMessage(new OtherFakeEvent3(3)).ExpectedSingle().Id.ShouldEqual(_otherPeer.Id);

            _directory.Handle(new PeerSubscriptionsForTypesUpdated(_otherPeer.Id, DateTime.UtcNow.AddTicks(2), new MessageTypeId(typeof(OtherFakeEvent3)), new BindingKey("3"), new BindingKey("4")));

            _directory.GetPeerDescriptor(_otherPeer.Id).Subscriptions.Length.ShouldEqual(3);
            _directory.GetPeersHandlingMessage(new OtherFakeEvent3(3)).ExpectedSingle().Id.ShouldEqual(_otherPeer.Id);
            _directory.GetPeersHandlingMessage(new OtherFakeEvent3(4)).ExpectedSingle().Id.ShouldEqual(_otherPeer.Id);

            _directory.Handle(new PeerSubscriptionsForTypesUpdated(_otherPeer.Id,DateTime.UtcNow.AddTicks(2), new MessageTypeId(typeof(OtherFakeEvent3)), new BindingKey("4")));

            _directory.GetPeerDescriptor(_otherPeer.Id).Subscriptions.Length.ShouldEqual(2);
            _directory.GetPeersHandlingMessage(new OtherFakeEvent3(3)).ShouldBeEmpty();
            _directory.GetPeersHandlingMessage(new OtherFakeEvent3(4)).ExpectedSingle().Id.ShouldEqual(_otherPeer.Id);
        }

        [Test]
        public void should_support_null_binding_keys_in_subscriptions_for_types()
        {
            _directory.Handle(new PeerStarted(_otherPeer.ToPeerDescriptor(true)));

            _directory.Handle(new PeerSubscriptionsForTypesUpdated(_otherPeer.Id, DateTime.UtcNow.AddTicks(1), new MessageTypeId(typeof(FakeEvent)), BindingKey.Empty));
            _directory.GetPeersHandlingMessage(new FakeEvent(0)).ShouldNotBeEmpty();

            Assert.DoesNotThrow(() => _directory.Handle(new PeerSubscriptionsForTypesUpdated(_otherPeer.Id, DateTime.UtcNow.AddTicks(1), new MessageTypeId(typeof(FakeEvent)), null)));

            _directory.GetPeersHandlingMessage(new FakeEvent(0)).ShouldBeEmpty();
        }

        [Test]
        public void should_ignore_outdated_subscriptions_by_type()
        {
            _directory.Handle(new PeerStarted(_otherPeer.ToPeerDescriptor(true, typeof(FakeEvent))));;
            _directory.Handle(new PeerSubscriptionsForTypesUpdated(_otherPeer.Id, DateTime.UtcNow.AddMinutes(1), new MessageTypeId(typeof(OtherFakeEvent3)), new BindingKey("3")));
            _directory.Handle(new PeerSubscriptionsForTypesUpdated(_otherPeer.Id, DateTime.UtcNow.AddTicks(1), new MessageTypeId(typeof(OtherFakeEvent3))));

            _directory.GetPeerDescriptor(_otherPeer.Id).Subscriptions.Length.ShouldEqual(2);
            _directory.GetPeersHandlingMessage(new OtherFakeEvent3(3)).ShouldNotBeEmpty();
        }

        [Test]
        public void should_not_ignore_subscriptions_received_while_registering()
        {
            _bus.AddHandler<RegisterPeerCommand>(x =>
            {
                var peerDescriptor = _otherPeer.ToPeerDescriptor(true);
                _directory.Handle(new PeerSubscriptionsForTypesUpdated(_otherPeer.Id, DateTime.UtcNow.AddTicks(1), new MessageTypeId(typeof(FakeEvent)), BindingKey.Empty));
                return new RegisterPeerResponse(new[] { peerDescriptor });
            });

            _directory.Register(_bus, _self, Enumerable.Empty<Subscription>());

            _directory.GetPeerDescriptor(_otherPeer.Id).Subscriptions.Length.ShouldEqual(1);
            _directory.GetPeersHandlingMessage(new FakeEvent(0)).ShouldNotBeEmpty();
        }

        [Test, Repeat(20)]
        public void should_handle_directory_events_during_the_register()
        {
            var otherPeerDescriptor = _otherPeer.ToPeerDescriptor(true, typeof(FakeCommand), typeof(FakeEvent));
            otherPeerDescriptor.TimestampUtc = default(DateTime);
            var peerStarted = new PeerStarted(otherPeerDescriptor);
            var peerStopped = new PeerStopped(_otherPeer);

            _bus.AddHandler<RegisterPeerCommand>(x =>
            {
                var peerDescriptor = _otherPeer.ToPeerDescriptor(true);
                _directory.Handle(new PeerSubscriptionsForTypesUpdated(_otherPeer.Id, DateTime.UtcNow.AddTicks(1), new MessageTypeId(typeof(FakeEvent)), BindingKey.Empty));
                return new RegisterPeerResponse(new[] { peerDescriptor });
            });

            var task = Task.Run(() =>
            {
                for (var i = 0; i < 10000; i++)
                {
                    _directory.Handle(peerStarted);
                    _directory.Handle(peerStopped);
                }
            });

            Wait.Until(() => task.Status == TaskStatus.Running || task.Status == TaskStatus.RanToCompletion, 1.Second());

           _directory.Register(_bus, _self, otherPeerDescriptor.Subscriptions );

            task.Wait(1.Second());
        }

        private class OtherFakeEvent1 : IEvent
        {
        }

        private class OtherFakeEvent2 : IEvent
        {
        }

        [Routable]
        private class OtherFakeEvent3 : IEvent
        {
            [RoutingPosition(1)]
            public readonly int Id;

            public OtherFakeEvent3(int id)
            {
                Id = id;
            }
        }
    }
}
