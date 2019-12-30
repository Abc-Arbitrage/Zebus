using NUnit.Framework;
using Abc.Zebus.Directory.RocksDb.Storage;
using Abc.Zebus.Testing.Extensions;
using System;
using System.Linq;
using System.Threading;

namespace Abc.Zebus.Directory.RocksDb.Tests
{
    [TestFixture]
    public partial class RocksDbPeerRepositoryTests
    {
        private Peer _peer1;
        private Peer _peer2;
        private RocksDbPeerRepository _repository;

        [SetUp]
        public void Setup()
        {
            _peer1 = new Peer(new PeerId("Abc.Peer.1"), "tcp://endpoint:123");
            _peer2 = new Peer(new PeerId("Abc.Peer.2"), "tcp://endpoint:123");
            _repository = new RocksDbPeerRepository();
            _repository.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _repository.Dispose();
            System.IO.Directory.Delete(_repository.DatabaseFilePath, true);
        }

        [Test]
        public void should_add_and_get_peer_descriptor()
        {
            var peerId = new PeerId("lol");
            var sourcePeerDescriptor = CreatePeerDescriptor(peerId);
            _repository.AddOrUpdatePeer(sourcePeerDescriptor);
            var peerDescriptor = _repository.Get(peerId);

            peerDescriptor.ShouldNotBeNull();
            peerDescriptor.ShouldEqualDeeply(sourcePeerDescriptor);
        }

        [Test]
        public void should_insert_and_get_peer()
        {
            var peerId = new PeerId("lol1");
            var peerDescriptor = CreatePeerDescriptor(peerId);
            peerDescriptor.HasDebuggerAttached = true;
            var otherPeerId = new PeerId("lol2");
            var otherDescriptor = CreatePeerDescriptor(otherPeerId);

            _repository.AddOrUpdatePeer(peerDescriptor);
            _repository.AddOrUpdatePeer(otherDescriptor);

            var peerFetched = _repository.Get(peerDescriptor.Peer.Id);
            var otherPeerFetched = _repository.Get(otherDescriptor.Peer.Id);
            peerFetched.ShouldHaveSamePropertiesAs(peerDescriptor);
            otherPeerFetched.ShouldHaveSamePropertiesAs(otherDescriptor);
        }

        [Test]
        public void should_update_peer()
        {
            var peerId = new PeerId("lol1");
            var peerDescriptor = CreatePeerDescriptor(peerId);
            _repository.AddOrUpdatePeer(peerDescriptor);

            var updatedPeer = CreatePeerDescriptor(peerId);
            updatedPeer.TimestampUtc = updatedPeer.TimestampUtc.Value.AddMilliseconds(1); // Ensures that the timestamps are different to prevent a conflict in Cassandra
            _repository.AddOrUpdatePeer(updatedPeer);

            var fetchedPeers = _repository.GetPeers();
            var fetchedPeer = fetchedPeers.Single();
            fetchedPeer.ShouldHaveSamePropertiesAs(updatedPeer);
        }

        [Test]
        public void should_remove_peer()
        {
            var peerDescriptor = CreatePeerDescriptor(_peer1.Id);

            _repository.AddOrUpdatePeer(peerDescriptor);
            _repository.RemovePeer(peerDescriptor.Peer.Id);

            var fetched = _repository.Get(peerDescriptor.Peer.Id);
            fetched.ShouldBeNull();
        }

        [Test]
        public void should_read_peer_after_removing_it()
        {
            var peerDescriptor = CreatePeerDescriptor(_peer1.Id);
            peerDescriptor.TimestampUtc = DateTime.UtcNow;

            _repository.AddOrUpdatePeer(peerDescriptor);
            _repository.RemovePeer(peerDescriptor.Peer.Id);
            peerDescriptor.TimestampUtc = peerDescriptor.TimestampUtc.Value.Add(TimeSpan.FromSeconds(1));
            _repository.AddOrUpdatePeer(peerDescriptor);

            var fetched = _repository.Get(peerDescriptor.Peer.Id);
            fetched.ShouldNotBeNull();
        }

        [Test]
        public void should_insert_a_peer_with_no_timestamp_that_was_previously_deleted()
        {
            var descriptor = CreatePeerDescriptor(_peer1.Id);
            descriptor.TimestampUtc = DateTime.UtcNow;
            _repository.AddOrUpdatePeer(descriptor);
            _repository.RemovePeer(descriptor.PeerId);

            Thread.Sleep(1);

            descriptor = CreatePeerDescriptor(_peer1.Id);
            descriptor.TimestampUtc = null;

            _repository.AddOrUpdatePeer(descriptor);

            var fetched = _repository.Get(_peer1.Id);
            fetched.ShouldNotBeNull();
        }

        [Test]
        public void should_mark_peer_as_responding()
        {
            var descriptor = CreatePeerDescriptor(_peer1.Id);
            descriptor.TimestampUtc = DateTime.UtcNow.AddTicks(-10);
            _repository.AddOrUpdatePeer(descriptor);

            _repository.SetPeerResponding(_peer1.Id, false);
            _repository.Get(_peer1.Id).Peer.IsResponding.ShouldBeFalse();
            _repository.GetPeers().ExpectedSingle().Peer.IsResponding.ShouldBeFalse();

            _repository.SetPeerResponding(_peer1.Id, true);
            _repository.Get(_peer1.Id).Peer.IsResponding.ShouldBeTrue();
            _repository.GetPeers().ExpectedSingle().Peer.IsResponding.ShouldBeTrue();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void get_persistent_state(bool isPersistent)
        {
            _repository.AddOrUpdatePeer(CreatePeerDescriptor(_peer1.Id, isPersistent:isPersistent));

            _repository.IsPersistent(_peer1.Id).ShouldEqual(isPersistent);
        }

        [Test]
        public void get_persistent_state_when_peer_does_not_exists()
        {
            _repository.IsPersistent(_peer1.Id).ShouldBeNull();
        }

        [Test]
        public void should_handle_peers_with_null_subscriptions_gracefully()
        {
            var descriptor = CreatePeerDescriptor(_peer1.Id);
            descriptor.TimestampUtc = DateTime.UtcNow;
            _repository.AddOrUpdatePeer(descriptor);

            /*DataContext.StoragePeers
                        .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                        .Where(peer => peer.UselessKey == false && peer.PeerId == "Abc.DecommissionnedPeer.0")
                        .Select(peer => new StoragePeer { StaticSubscriptionsBytes  = null, IsResponding = false, IsPersistent = false, HasDebuggerAttached = false, IsUp = false })
                        .Update()
                        .SetTimestamp(DateTime.UtcNow)
                        .Execute();*/

            _repository.Get(_peer1.Id).Peer.IsResponding.ShouldBeTrue();
            _repository.GetPeers().ExpectedSingle().PeerId.ShouldEqual(_peer1.Id);

            throw new NotImplementedException();
        }

        private static PeerDescriptor CreatePeerDescriptor(PeerId peerId, bool isPersistent = true, bool isResponding = true)
        {
            return new PeerDescriptor(peerId, "endpoint", isPersistent, true, isResponding, DateTime.UtcNow, new[] { Subscription.Any<TestMessage>() });
        }

        class TestMessage : IMessage { }
    }
}
