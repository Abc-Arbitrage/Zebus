using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.CQL.Data;
using Abc.Zebus.Persistence.CQL.Storage;
using Abc.Zebus.Persistence.CQL.Tests.Cql;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.CQL.Tests
{
    public class PeerStateRepositoryTests : CqlTestFixture<PersistenceCqlDataContext, ICqlPersistenceConfiguration>
    {
        private PeerStateRepository _peerStateRepository;

        public override void CreateSchema()
        {
            IgnoreOnAppVeyor();
            base.CreateSchema();
        }

        private void IgnoreOnAppVeyor()
        {
            var env = Environment.GetEnvironmentVariable("APPVEYOR");
            bool isUnderAppVeyor;
            if (!string.IsNullOrEmpty(env) && bool.TryParse(env, out isUnderAppVeyor) && isUnderAppVeyor)
                Assert.Ignore("We need a cassandra node for this");
        }

        [SetUp]
        public void SetUp()
        {
            _peerStateRepository = new PeerStateRepository(DataContext);
        }

        [Test]
        public void update_non_acked_message_count_should_create_the_peer_state_if_not_exists_yet()
        {
            _peerStateRepository.UpdateNonAckMessageCount(new PeerId("PeerId"), 10);

            _peerStateRepository.ShouldNotBeEmpty();
            _peerStateRepository[new PeerId("PeerId")].ShouldNotBeNull();
        }

        [Test]
        public void should_update_non_acked_message_count()
        {
            _peerStateRepository.UpdateNonAckMessageCount(new PeerId("PeerId"), 10);

            _peerStateRepository[new PeerId("PeerId")].NonAckedMessageCount.ShouldEqual(10);
        }

        [Test]
        public void should_update_last_count_change_when_changing_count()
        {
            _peerStateRepository.UpdateNonAckMessageCount(new PeerId("PeerId"), 10);

            _peerStateRepository[new PeerId("PeerId")].LastNonAckedMessageCountVersion.ShouldBeGreaterThan(0);
        }

        [Test]
        public void peer_state_should_be_deleted_from_repository_when_removed()
        {
            var peerId = new PeerId("PeerId");
            _peerStateRepository.UpdateNonAckMessageCount(peerId, 10);
            _peerStateRepository.RemovePeer(peerId);

            _peerStateRepository.ShouldBeEmpty();
            _peerStateRepository.Any(x => x.PeerId == peerId).ShouldBeFalse();
        }

        [Test]
        public async Task should_save_state_to_cassandra()
        {
            using (SystemDateTime.PauseTime())
            {
                _peerStateRepository.UpdateNonAckMessageCount(new PeerId("PeerId"), 10);

                await _peerStateRepository.Save();

                var oldestNonAckedMessageTimestampCaptured = SystemDateTime.UtcNow - CqlStorage.PersistentMessagesTimeToLive;

                var cassandraState = DataContext.PeerStates.Execute().ExpectedSingle();
                cassandraState.PeerId.ShouldEqual("PeerId");
                cassandraState.NonAckedMessageCount.ShouldEqual(10);
                cassandraState.OldestNonAckedMessageTimestamp.ShouldEqual(oldestNonAckedMessageTimestampCaptured.Ticks);
            }
        }

        [Test]
        public async Task should_reload_state_from_cassandra_on_initialize()
        {
            using (SystemDateTime.PauseTime())
            {
                _peerStateRepository.UpdateNonAckMessageCount(new PeerId("PeerId"), 10);

                await _peerStateRepository.Save();

                var oldestNonAckedMessageTimestampCaptured = SystemDateTime.UtcNow - CqlStorage.PersistentMessagesTimeToLive;

                using (SystemDateTime.Set(utcNow: SystemDateTime.UtcNow.Add(2.Hours())))
                {
                    var newRepo = new PeerStateRepository(DataContext);
                    newRepo.Initialize();

                    var cassandraState = newRepo.ExpectedSingle();
                    cassandraState.PeerId.ShouldEqual(new PeerId("PeerId"));
                    cassandraState.NonAckedMessageCount.ShouldEqual(10);
                    cassandraState.OldestNonAckedMessageTimestampInTicks.ShouldEqual(oldestNonAckedMessageTimestampCaptured.Ticks);
                }
            }
        }

        [Test]
        public async Task should_delete_peer_from_cassandra_when_removed()
        {
            using (SystemDateTime.PauseTime())
            {
                _peerStateRepository.UpdateNonAckMessageCount(new PeerId("PeerId"), 10);
                await _peerStateRepository.Save();

                DataContext.PeerStates.Execute().ShouldNotBeEmpty();

                await _peerStateRepository.RemovePeer(new PeerId("PeerId"));

                DataContext.PeerStates.Execute().ShouldBeEmpty();
            }
        }

        [Test]
        public void should_get_updated_peers_only_for_the_peers_that_actually_changed_since_the_last_publication()
        {
            var version = 0L;

            _peerStateRepository.UpdateNonAckMessageCount(new PeerId("PeerId.1"), 10);
            _peerStateRepository.UpdateNonAckMessageCount(new PeerId("PeerId.2"), 20);

            var peers1 = _peerStateRepository.GetUpdatedPeers(ref version).Select(x => x.PeerId.ToString()).ToList();
            peers1.ShouldBeEquivalentTo(new[] { "PeerId.1", "PeerId.2" });

            _peerStateRepository.UpdateNonAckMessageCount(new PeerId("PeerId.1"), 2);

            var peers2 = _peerStateRepository.GetUpdatedPeers(ref version).Select(x => x.PeerId.ToString()).ToList();
            peers2.ShouldBeEquivalentTo(new[] { "PeerId.1" });
        }

        [Test]
        public async Task should_delete_all_buckets_for_peer_when_removed()
        {
            var now = SystemDateTime.UtcNow;
            _peerStateRepository.UpdateNonAckMessageCount(new PeerId("PeerId"), 0);
            DataContext.PersistentMessages.Insert(new PersistentMessage
            {
                BucketId = BucketIdHelper.GetBucketId(now),
                IsAcked = true,
                PeerId = "PeerId",
                TransportMessage = new byte[0],
                UniqueTimestampInTicks = now.Ticks
            }).Execute();

            DataContext.PersistentMessages.Execute().Count().ShouldEqual(1);

            await _peerStateRepository.RemovePeer(new PeerId("PeerId"));

            DataContext.PersistentMessages.Execute().Any().ShouldBeFalse();
        }
    }
}
