using System.Linq;
using Abc.Zebus.Directory.Storage;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Directory.Tests.Storage
{
    [TestFixture]
    public class MemoryPeerRepositoryTests
    {
        private MemoryPeerRepository _repository;
        private Peer _peer1;
        private Peer _peer2;

        [SetUp]
        public void Setup()
        {
            _repository = new MemoryPeerRepository();
            _peer1 = new Peer(new PeerId("Abc.Peer.1"), "tcp://endpoint:123");
            _peer2 = new Peer(new PeerId("Abc.Peer.2"), "tcp://endpoint:456");
        }

        [Test]
        public void should_insert_and_get_peer()
        {
            var peerDescriptor = _peer1.ToPeerDescriptor(true, typeof(FakeCommand));

            _repository.AddOrUpdatePeer(peerDescriptor);

            var fetched = _repository.Get(peerDescriptor.Peer.Id);
            fetched.ShouldHaveSamePropertiesAs(peerDescriptor);
        }

        [Test]
        public void should_return_null_when_peer_does_not_exists()
        {
            var fetched = _repository.Get(new PeerId("PeerId"));

            fetched.ShouldBeNull();
        }

        [Test]
        public void should_get_all_peers()
        {
            var peerDescriptor1 = _peer1.ToPeerDescriptor(true, typeof(string));
            peerDescriptor1.HasDebuggerAttached = true;
            _repository.AddOrUpdatePeer(peerDescriptor1);

            var peerDescriptor2 = _peer2.ToPeerDescriptor(true, typeof(int));
            _repository.AddOrUpdatePeer(peerDescriptor2);

            var fetchedPeers = _repository.GetPeers().ToList();

            var fetchedPeer1 = fetchedPeers.Single(x => x.Peer.Id == peerDescriptor1.Peer.Id);
            fetchedPeer1.ShouldHaveSamePropertiesAs(peerDescriptor1);
            var fetchedPeer2 = fetchedPeers.Single(x => x.Peer.Id == peerDescriptor2.Peer.Id);
            fetchedPeer2.ShouldHaveSamePropertiesAs(peerDescriptor2);
        }

        [Test]
        public void should_update_peer()
        {
            var peerDescriptor = _peer1.ToPeerDescriptor(true, typeof(string));
            _repository.AddOrUpdatePeer(peerDescriptor);

            var updatedPeer = _peer1.ToPeerDescriptor(false, typeof(int));
            _repository.AddOrUpdatePeer(updatedPeer);

            var fetchedPeers = _repository.GetPeers();
            var fetchedPeer = fetchedPeers.Single();
            fetchedPeer.ShouldHaveSamePropertiesAs(updatedPeer);
        }

        [Test]
        public void should_not_override_peer_with_old_version()
        {
            var descriptor1 = _peer1.ToPeerDescriptor(true, typeof(FakeCommand));
            descriptor1.TimestampUtc = SystemDateTime.UtcNow.AddMinutes(1);
            _repository.AddOrUpdatePeer(descriptor1);

            var descriptor2 = _peer1.ToPeerDescriptor(true);
            _repository.AddOrUpdatePeer(descriptor2);

            var fetched = _repository.Get(_peer1.Id);
            fetched.TimestampUtc.ShouldEqual(descriptor1.TimestampUtc);
            fetched.Subscriptions.ShouldBeEquivalentTo(descriptor1.Subscriptions);
        }

        [Test]
        public void should_remove_peer()
        {
            var peerDescriptor = _peer1.ToPeerDescriptor(true, typeof(string));

            _repository.AddOrUpdatePeer(peerDescriptor);
            _repository.RemovePeer(peerDescriptor.Peer.Id);

            var fetched = _repository.Get(peerDescriptor.Peer.Id);
            fetched.ShouldBeNull();
        }

        [Test]
        public void should_insert_a_peer_with_no_timestamp_that_was_previously_deleted()
        {
            var descriptor = _peer1.ToPeerDescriptor(true);
            descriptor.TimestampUtc = null;

            _repository.AddOrUpdatePeer(descriptor);

            var fetched = _repository.Get(_peer1.Id);
            fetched.ShouldNotBeNull();
        }
    }
}
