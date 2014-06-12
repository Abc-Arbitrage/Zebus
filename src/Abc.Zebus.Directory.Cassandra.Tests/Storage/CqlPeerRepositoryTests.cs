using Abc.Zebus.Directory.Cassandra.Cql;
using Abc.Zebus.Directory.Cassandra.Storage;
using Abc.Zebus.Directory.Cassandra.Tests.Cql;
using Abc.Zebus.Directory.Tests;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;

namespace Abc.Zebus.Directory.Cassandra.Tests.Storage
{
    [TestFixture]
    public class CqlPeerRepositoryTests : CqlTestFixture<DirectoryDataContext, ICassandraConfiguration>
    {
        private CqlPeerRepository _repository;
        private Peer _peer1;
        private Peer _peer2;

        protected override string Hosts { get { return "test_cassandra"; } }

        protected override string LocalDataCenter { get { return "Paris-CEN"; } }
        
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
            var peerDescriptor = _peer1.ToPeerDescriptor(true, typeof(FakeCommand));
            peerDescriptor.HasDebuggerAttached = true;
            var otherDescriptor = _peer2.ToPeerDescriptor(false, typeof(FakeCommand), typeof(FakeRoutableCommand));

            _repository.AddOrUpdatePeer(peerDescriptor);
            _repository.AddOrUpdatePeer(otherDescriptor);

            var peerFetched = _repository.Get(peerDescriptor.Peer.Id);
            var otherPeerFetched = _repository.Get(otherDescriptor.Peer.Id);
            peerFetched.ShouldHaveSamePropertiesAs(peerDescriptor);
            otherPeerFetched.ShouldHaveSamePropertiesAs(otherDescriptor);
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
            descriptor1.TimestampUtc = SystemDateTime.UtcNow.AddMinutes(1).RoundToMillisecond();
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
        public void should_add_dynamic_subscriptions()
        {
            throw new NotImplementedException();
        }

        [Test]
        public void should_remove_dynamic_subscriptions()
        {
            throw new NotImplementedException();
        }

        [Test, Ignore("Will be implemented for incremental subscriptions")]
        public void should_remove_the_dynamic_subscriptions_of_a_peer_when_removing_it()
        {
            throw new NotImplementedException();
        }

        [Test, Ignore("Will be implemented for incremental subscriptions")]
        public void should_not_override_subscriptions_with_old_version()
        {
            throw new NotImplementedException();
        }

        [Test, Ignore("Will be implemented for incremental subscriptions")]
        public void should_get_a_peer_with_no_subscriptions_using_getAll()
        {
            throw new NotImplementedException();
        }

        [Test, Ignore("Will be implemented for incremental subscriptions")]
        public void parts_should_stay_in_order()
        {
            throw new NotImplementedException();
        }

        [Test]
        public void removing_a_dynamic_subscription_doesnt_remove_static_subscription()
        {
            throw new NotImplementedException();
        }

        [Test]
        public void should_deduplicate_dynamic_subscriptions()
        {
            throw new NotImplementedException();
        }
        
        [Test]
        public void should_not_mixup_subscriptions_to_same_type_with_different_tokens()
        {
            throw new NotImplementedException();
        }

        [Test]
        public void should_not_erase_subscriptions_of_a_peer_on_register()
        {
            // Allow reregister
            throw new NotImplementedException();
        }

        [Test]
        public void should_insert_a_peer_with_no_timestamp_that_was_previously_deleted()
        {
            var descriptor = _peer1.ToPeerDescriptor(true);
            descriptor.TimestampUtc = DateTime.UtcNow;
            _repository.AddOrUpdatePeer(descriptor);
            _repository.RemovePeer(descriptor.PeerId);

            Thread.Sleep(1);

            descriptor = _peer1.ToPeerDescriptor(true);
            descriptor.TimestampUtc = null;

            _repository.AddOrUpdatePeer(descriptor);

            var fetched = _repository.Get(_peer1.Id);
            fetched.ShouldNotBeNull();
        }

        [Test]
        public void should_mark_peer_as_responding()
        {
            var descriptor = _peer1.ToPeerDescriptor(true);
            descriptor.TimestampUtc = DateTime.UtcNow;
            _repository.AddOrUpdatePeer(descriptor);

            _repository.SetPeerResponding(_peer1.Id, false);
            _repository.Get(_peer1.Id).Peer.IsResponding.ShouldBeFalse();
            _repository.GetPeers().ExpectedSingle().Peer.IsResponding.ShouldBeFalse();

            _repository.SetPeerResponding(_peer1.Id, true);
            _repository.Get(_peer1.Id).Peer.IsResponding.ShouldBeTrue();
            _repository.GetPeers().ExpectedSingle().Peer.IsResponding.ShouldBeTrue();
        }
    }
}