using Abc.Zebus.Directory.Cassandra.Cql;
using Abc.Zebus.Directory.Cassandra.Storage;
using Abc.Zebus.Directory.Cassandra.Tests.Cql;
using Abc.Zebus.Directory.Tests;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using Cassandra;
using Cassandra.Data.Linq;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;

namespace Abc.Zebus.Directory.Cassandra.Tests.Storage
{
    [TestFixture]
    public partial class CqlPeerRepositoryTests : CqlTestFixture<DirectoryDataContext, ICassandraConfiguration>
    {
        private CqlPeerRepository _repository;
        private Peer _peer1;
        private Peer _peer2;

        public override void CreateSchema()
        {
            IgnoreWhenSet("GITHUB_ACTIONS");
            base.CreateSchema();
        }

        private static void IgnoreWhenSet(string environmentVariable)
        {
            var env = Environment.GetEnvironmentVariable(environmentVariable);
            if (!string.IsNullOrEmpty(env) && bool.TryParse(env, out var isSet) && isSet)
                Assert.Ignore("We need a cassandra node for this");
        }

        protected override string Hosts => "cassandra-test";
        protected override string LocalDataCenter => "Paris-ABC";

        [SetUp]
        protected void Setup()
        {
            _repository = new CqlPeerRepository(DataContext);
            _peer1 = new Peer(new PeerId("Abc.Peer.1"), "tcp://endpoint:123");
            _peer2 = new Peer(new PeerId("Abc.Peer.2"), "tcp://endpoint:456");
        }

        [Test]
        public void should_insert_and_get_peer()
        {
            var peerDescriptor = _peer1.ToPeerDescriptorWithRoundedTime(true, typeof(FakeCommand));
            peerDescriptor.HasDebuggerAttached = true;
            var otherDescriptor = _peer2.ToPeerDescriptorWithRoundedTime(false, typeof(FakeCommand), typeof(FakeRoutableCommand));

            _repository.AddOrUpdatePeer(peerDescriptor);
            _repository.AddOrUpdatePeer(otherDescriptor);

            var peerFetched = _repository.Get(peerDescriptor.Peer.Id);
            var otherPeerFetched = _repository.Get(otherDescriptor.Peer.Id);
            peerFetched.ShouldHaveSamePropertiesAs(peerDescriptor);
            otherPeerFetched.ShouldHaveSamePropertiesAs(otherDescriptor);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void get_persistent_state(bool isPersistent)
        {
            _repository.AddOrUpdatePeer(_peer1.ToPeerDescriptor(isPersistent));

            _repository.IsPersistent(_peer1.Id).ShouldEqual(isPersistent);
        }

        [Test]
        public void get_persistent_state_when_peer_does_not_exists()
        {
            _repository.IsPersistent(_peer1.Id).ShouldBeNull();
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
            var peerDescriptor1 = _peer1.ToPeerDescriptorWithRoundedTime(true, typeof(string));
            peerDescriptor1.HasDebuggerAttached = true;
            _repository.AddOrUpdatePeer(peerDescriptor1);

            var peerDescriptor2 = _peer2.ToPeerDescriptorWithRoundedTime(true, typeof(int));
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
            var peerDescriptor = _peer1.ToPeerDescriptorWithRoundedTime(true, typeof(string));
            _repository.AddOrUpdatePeer(peerDescriptor);

            var updatedPeer = _peer1.ToPeerDescriptorWithRoundedTime(false, typeof(int));
            updatedPeer.TimestampUtc = updatedPeer.TimestampUtc.Value.AddMilliseconds(1); // Ensures that the timestamps are different to prevent a conflict in Cassandra
            _repository.AddOrUpdatePeer(updatedPeer);

            var fetchedPeers = _repository.GetPeers();
            var fetchedPeer = fetchedPeers.Single();
            fetchedPeer.ShouldHaveSamePropertiesAs(updatedPeer);
        }

        [Test]
        public void should_not_override_peer_with_old_version()
        {
            var descriptor1 = _peer1.ToPeerDescriptorWithRoundedTime(true, typeof(FakeCommand));
            descriptor1.TimestampUtc = SystemDateTime.UtcNow.AddMinutes(1).RoundToMillisecond();
            _repository.AddOrUpdatePeer(descriptor1);

            var descriptor2 = _peer1.ToPeerDescriptorWithRoundedTime(true);
            _repository.AddOrUpdatePeer(descriptor2);

            var fetched = _repository.Get(_peer1.Id);
            fetched.TimestampUtc.ShouldEqual(descriptor1.TimestampUtc);
            fetched.Subscriptions.ShouldBeEquivalentTo(descriptor1.Subscriptions);
        }

        [Test]
        public void should_remove_peer()
        {
            var peerDescriptor = _peer1.ToPeerDescriptorWithRoundedTime(true, typeof(string));

            _repository.AddOrUpdatePeer(peerDescriptor);
            _repository.RemovePeer(peerDescriptor.Peer.Id);

            var fetched = _repository.Get(peerDescriptor.Peer.Id);
            fetched.ShouldBeNull();
        }

        [Test]
        public void should_read_peer_after_removing_it()
        {
            var peerDescriptor = _peer1.ToPeerDescriptorWithRoundedTime(true, typeof(string));
            peerDescriptor.TimestampUtc = GetUnspecifiedKindUtcNow();

            _repository.AddOrUpdatePeer(peerDescriptor);
            _repository.RemovePeer(peerDescriptor.Peer.Id);
            peerDescriptor.TimestampUtc = peerDescriptor.TimestampUtc.Value.Add(1.Second());
            _repository.AddOrUpdatePeer(peerDescriptor);

            var fetched = _repository.Get(peerDescriptor.Peer.Id);
            fetched.ShouldNotBeNull();
        }

        private static DateTime GetUnspecifiedKindUtcNow()
        {
            return new DateTime(SystemDateTime.UtcNow.RoundToMillisecond().Ticks, DateTimeKind.Unspecified);
        }

        [Test]
        public void should_insert_a_peer_with_no_timestamp_that_was_previously_deleted()
        {
            var descriptor = _peer1.ToPeerDescriptorWithRoundedTime(true);
            descriptor.TimestampUtc = DateTime.UtcNow;
            _repository.AddOrUpdatePeer(descriptor);
            _repository.RemovePeer(descriptor.PeerId);

            Thread.Sleep(1);

            descriptor = _peer1.ToPeerDescriptorWithRoundedTime(true);
            descriptor.TimestampUtc = null;

            _repository.AddOrUpdatePeer(descriptor);

            var fetched = _repository.Get(_peer1.Id);
            fetched.ShouldNotBeNull();
        }

        [Test]
        public void should_mark_peer_as_responding()
        {
            var descriptor = _peer1.ToPeerDescriptorWithRoundedTime(true);
            descriptor.TimestampUtc = DateTime.UtcNow.AddTicks(-10);
            _repository.AddOrUpdatePeer(descriptor);

            _repository.SetPeerResponding(_peer1.Id, false);
            _repository.Get(_peer1.Id).Peer.IsResponding.ShouldBeFalse();
            _repository.GetPeers().ExpectedSingle().Peer.IsResponding.ShouldBeFalse();

            _repository.SetPeerResponding(_peer1.Id, true);
            _repository.Get(_peer1.Id).Peer.IsResponding.ShouldBeTrue();
            _repository.GetPeers().ExpectedSingle().Peer.IsResponding.ShouldBeTrue();
        }

        [Test]
        public void should_handle_peers_with_null_subscriptions_gracefully()
        {
            var descriptor = _peer1.ToPeerDescriptorWithRoundedTime(true);
            descriptor.TimestampUtc = DateTime.UtcNow;
            _repository.AddOrUpdatePeer(descriptor);

            DataContext.StoragePeers
                        .SetConsistencyLevel(ConsistencyLevel.LocalQuorum)
                        .Where(peer => peer.UselessKey == false && peer.PeerId == "Abc.DecommissionnedPeer.0")
                        .Select(peer => new StoragePeer { StaticSubscriptionsBytes  = null, IsResponding = false, IsPersistent = false, HasDebuggerAttached = false, IsUp = false })
                        .Update()
                        .SetTimestamp(DateTime.UtcNow)
                        .Execute();

            _repository.Get(_peer1.Id).Peer.IsResponding.ShouldBeTrue();
            _repository.GetPeers().ExpectedSingle().PeerId.ShouldEqual(_peer1.Id);
        }
    }
}
