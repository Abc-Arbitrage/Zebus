using System.Linq;
using Abc.Zebus.Directory.Storage;
using Abc.Zebus.Routing;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Comparison;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using Abc.Zebus;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Directory.Tests.Directory
{
    [TestFixture]
    public class PeerDirectoryServerTests
    {
        private PeerDirectoryServer _peerDirectory;
        private Mock<IPeerRepository> _repositoryMock;
        private TestBus _bus;
        private UpdatedPeer _updatedPeer;
        private Peer _self;
        private Peer _otherPeer;

        [SetUp]
        public void Setup()
        {
            _repositoryMock = new Mock<IPeerRepository>();
            _peerDirectory = new PeerDirectoryServer(_repositoryMock.Object);

            _updatedPeer = null;
            _peerDirectory.PeerUpdated += (id, action) => _updatedPeer = new UpdatedPeer(id, action);

            _bus = new TestBus();

            _self = new Peer(new PeerId("Abc.DirectoryService.0"), "tcp://abc:42");
            _otherPeer = new Peer(new PeerId("Abc.Testing.0"), "tcp://abc:123");
        }

        [Test]
        public void register_persist_state_and_advertise()
        {
            using (SystemDateTime.PauseTime())
            using (SystemDateTime.Set(SystemDateTime.Now))
            {
                var peerDescriptor = _self.ToPeerDescriptor(false, typeof(FakeCommand));

                _peerDirectory.Register(_bus, peerDescriptor.Peer, peerDescriptor.Subscriptions);

                _bus.ExpectExactly(new PeerStarted(peerDescriptor));
                _repositoryMock.Verify(x => x.AddOrUpdatePeer(It.Is<PeerDescriptor>(descriptor => peerDescriptor.DeepCompare(descriptor))));
            }
        }

        [Test]
        public void should_raise_registered_event()
        {
            var peerDescriptor = _self.ToPeerDescriptor(true, typeof(FakeCommand));

            var raised = false;
            _peerDirectory.Registered += () => raised = true;

            _peerDirectory.Register(_bus, peerDescriptor.Peer, new Subscription[0]);

            raised.ShouldBeTrue();
        }

        [Test]
        public void unregister_should_persist_state_and_advertise()
        {
            using (SystemDateTime.PauseTime())
            using (SystemDateTime.Set(SystemDateTime.Now))
            {
                var peerDescriptor = _self.ToPeerDescriptor(true, typeof(FakeCommand));

                _repositoryMock.Setup(repo => repo.Get(It.Is<PeerId>(id => peerDescriptor.Peer.Id.Equals(id)))).Returns(peerDescriptor);
                _peerDirectory.Register(_bus, peerDescriptor.Peer, peerDescriptor.Subscriptions);

                _peerDirectory.Unregister(_bus);

                _bus.Expect(new[] { new PeerStopped(peerDescriptor.Peer) });
                _repositoryMock.Verify(repo => repo.Get(It.Is<PeerId>(id => peerDescriptor.Peer.Id.Equals(id))));
                _repositoryMock.Verify(repo => repo.AddOrUpdatePeer(It.Is<PeerDescriptor>(descriptor => peerDescriptor.DeepCompare(descriptor))));
            }
        }

        [Test]
        public void should_retrieve_the_peers_from_the_repository()
        {
            _repositoryMock.Setup(repo => repo.GetPeers())
                           .Returns(new[]
                           {
                               TestDataBuilder.CreatePersistentPeerDescriptor("tcp://goodapple:123", typeof(FakeCommand)),
                               TestDataBuilder.CreatePersistentPeerDescriptor("tcp://badapple:123", typeof(string))
                           });

            var peersHandlingFakeCommand = _peerDirectory.GetPeersHandlingMessage(new FakeCommand()).ToList();

            peersHandlingFakeCommand.Count().ShouldEqual(1);
            peersHandlingFakeCommand.First().EndPoint.ShouldEqual("tcp://goodapple:123");
        }

        [Test]
        public void should_get_peer_with_matching_subscrition_binding_key()
        {
            var command = new FakeRoutableCommand(10, "u.name");

            var peerDescriptor = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://goodapple:123", new Subscription(command.TypeId(), new BindingKey("10", "#")));
            _repositoryMock.Setup(x => x.GetPeers()).Returns(new[] { peerDescriptor });

            var peer = _peerDirectory.GetPeersHandlingMessage(command).Single();
            peer.Id.ShouldEqual(peerDescriptor.Peer.Id);
        }

        [Test]
        public void should_not_get_peer_with_non_matching_subscrition_binding_key()
        {
            var command = new FakeRoutableCommand(10, "u.name");

            var peerDescriptor = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://goodapple:123", new Subscription(command.TypeId(), new BindingKey("12", "#")));
            _repositoryMock.Setup(x => x.GetPeers()).Returns(new[] { peerDescriptor });

            _peerDirectory.GetPeersHandlingMessage(command).ShouldBeEmpty();
        }

        [Test]
        public void should_get_peer_by_id()
        {
            var peerDescriptor = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://badapple:123", typeof(string));
            _repositoryMock.Setup(repo => repo.Get(peerDescriptor.Peer.Id)).Returns(peerDescriptor);

            var fetchedPeerDescriptor = _peerDirectory.GetPeerDescriptor(peerDescriptor.Peer.Id);

            fetchedPeerDescriptor.ShouldEqualDeeply(peerDescriptor);
        }

        [Test]
        public void should_get_all_peers()
        {
            var peerDescriptor = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://badapple:123", typeof(string));
            _repositoryMock.Setup(repo => repo.GetPeers()).Returns(new[] { peerDescriptor });

            var fetchedPeerDescriptor = _peerDirectory.GetPeerDescriptors();

            fetchedPeerDescriptor.Single().ShouldEqualDeeply(peerDescriptor);
        }

        [Test]
        public void should_update_subscriptions()
        {
            var peerDescriptor = TestDataBuilder.CreatePersistentPeerDescriptor("tcp://lala:123");
            _peerDirectory.Register(_bus, peerDescriptor.Peer, peerDescriptor.Subscriptions);

            _repositoryMock.Setup(x => x.Get(peerDescriptor.Peer.Id)).Returns(peerDescriptor);

            var subscriptions = new[] { new Subscription(new MessageTypeId(typeof(FakeCommand))) };
            using (SystemDateTime.PauseTime())
            using (SystemDateTime.Set(SystemDateTime.Now))
            {
                _peerDirectory.Update(_bus, subscriptions);

                var expectedPeerDescriptor = peerDescriptor.Peer.ToPeerDescriptor(peerDescriptor.IsPersistent, subscriptions);
                _repositoryMock.Verify(x => x.AddOrUpdatePeer(It.Is<PeerDescriptor>(descriptor => descriptor.DeepCompare(expectedPeerDescriptor))));

                var updatedEvent = _bus.Events.OfType<PeerSubscriptionsUpdated>().Single();
                updatedEvent.PeerDescriptor.ShouldEqualDeeply(expectedPeerDescriptor);
            }
        }

        [Test]
        public void should_raise_peer_updated_after_peer_started()
        {
            _peerDirectory.Handle(new PeerStarted(_otherPeer.ToPeerDescriptor(true)));

            _updatedPeer.ShouldNotBeNull();
            _updatedPeer.PeerId.ShouldEqual(_otherPeer.Id);
            _updatedPeer.Action.ShouldEqual(PeerUpdateAction.Started);
        }

        [Test]
        public void should_raise_peer_updated_after_peer_stopped()
        {
            _peerDirectory.Handle(new PeerStopped(_otherPeer));

            _updatedPeer.ShouldNotBeNull();
            _updatedPeer.PeerId.ShouldEqual(_otherPeer.Id);
            _updatedPeer.Action.ShouldEqual(PeerUpdateAction.Stopped);
        }
        
        [Test]
        public void should_raise_peer_updated_after_peer_decommissioned()
        {
            _peerDirectory.Handle(new PeerDecommissioned(_otherPeer.Id));

            _updatedPeer.ShouldNotBeNull();
            _updatedPeer.PeerId.ShouldEqual(_otherPeer.Id);
            _updatedPeer.Action.ShouldEqual(PeerUpdateAction.Decommissioned);
        }
        
        [Test]
        public void should_raise_peer_updated_after_peer_subscription_updated()
        {
            _peerDirectory.Handle(new PeerSubscriptionsUpdated(_otherPeer.ToPeerDescriptor(true)));

            _updatedPeer.ShouldNotBeNull();
            _updatedPeer.PeerId.ShouldEqual(_otherPeer.Id);
            _updatedPeer.Action.ShouldEqual(PeerUpdateAction.Updated);
        }

        [Test]
        public void should_raise_peer_updated_after_peer_not_responding()
        {
            _peerDirectory.Handle(new PeerNotResponding(_otherPeer.Id));

            _updatedPeer.ShouldNotBeNull();
            _updatedPeer.PeerId.ShouldEqual(_otherPeer.Id);
            _updatedPeer.Action.ShouldEqual(PeerUpdateAction.Updated);
        }

        [Test]
        public void should_raise_peer_updated_after_peer_responding()
        {
            _peerDirectory.Handle(new PeerResponding(_otherPeer.Id));

            _updatedPeer.ShouldNotBeNull();
            _updatedPeer.PeerId.ShouldEqual(_otherPeer.Id);
            _updatedPeer.Action.ShouldEqual(PeerUpdateAction.Updated);
        }

        private class UpdatedPeer
        {
            public readonly PeerId PeerId;
            public readonly PeerUpdateAction Action;

            public UpdatedPeer(PeerId peerId, PeerUpdateAction action)
            {
                PeerId = peerId;
                Action = action;
            }
        }
    }
}