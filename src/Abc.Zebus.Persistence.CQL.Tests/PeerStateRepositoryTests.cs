using System.Linq;
using Abc.Zebus.Persistence.CQL.Data;
using Abc.Zebus.Persistence.CQL.Storage;
using Abc.Zebus.Persistence.CQL.Tests.Cql;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Persistence.Util;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.CQL.Tests
{
    [Ignore("We need a cassandra node for this")]
    public class PeerStateRepositoryTests : CqlTestFixture<PersistenceCqlDataContext, ICqlPersistenceConfiguration>
    {
        private TestBus _bus;
        private PeerStateRepository _peerStateRepository;

        [SetUp]
        public void SetUp()
        {
            _bus = new TestBus();
            _peerStateRepository = new PeerStateRepository(DataContext, _bus);
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
            using (SystemDateTime.PauseTime())
            {
                _peerStateRepository.UpdateNonAckMessageCount(new PeerId("PeerId"), 10);

                _peerStateRepository[new PeerId("PeerId")].LastNonAckedMessageCountChanged.ShouldEqual(SystemDateTime.UtcNow);
            }
        }

        [Test]
        public void peer_state_should_be_deleted_from_repository_when_purged()
        {
            var peerId = new PeerId("PeerId");
            _peerStateRepository.UpdateNonAckMessageCount(peerId, 10);
            _peerStateRepository.Purge(peerId);

            _peerStateRepository.ShouldBeEmpty();
            _peerStateRepository.Any(x=>x.PeerId==peerId).ShouldBeFalse();
        }
        
        [Test]
        public void should_save_state_to_cassandra()
        {
            using (SystemDateTime.PauseTime())
            {
                _peerStateRepository.UpdateNonAckMessageCount(new PeerId("PeerId"), 10);
                _peerStateRepository.Save().Wait();
                var oldestNonAckedMessageTimestampCaptured = SystemDateTime.UtcNow - CqlStorage.PersistentMessagesTimeToLive;

                var cassandraState = DataContext.PeerStates.Execute().ExpectedSingle();
                cassandraState.PeerId.ShouldEqual("PeerId");
                cassandraState.NonAckedMessageCount.ShouldEqual(10);
                cassandraState.OldestNonAckedMessageTimestamp.ShouldEqual(oldestNonAckedMessageTimestampCaptured.Ticks);
            }
        }

        [Test]
        public void should_reload_state_from_cassandra_on_initialize()
        {
            using (SystemDateTime.PauseTime())
            {
                _peerStateRepository.UpdateNonAckMessageCount(new PeerId("PeerId"), 10);
                _peerStateRepository.Save().Wait();
                var oldestNonAckedMessageTimestampCaptured = SystemDateTime.UtcNow - CqlStorage.PersistentMessagesTimeToLive;

                using (SystemDateTime.Set(utcNow: SystemDateTime.UtcNow.Add(2.Hours())))
                {
                    var newRepo = new PeerStateRepository(DataContext, _bus);
                    newRepo.Initialize();

                    var cassandraState = newRepo.ExpectedSingle();
                    cassandraState.PeerId.ShouldEqual(new PeerId("PeerId"));
                    cassandraState.NonAckedMessageCount.ShouldEqual(10);
                    cassandraState.OldestNonAckedMessageTimestampInTicks.ShouldEqual(oldestNonAckedMessageTimestampCaptured.Ticks);
                }
            }
        }

        [Test]
        public void should_delete_peer_from_cassandra_when_purged()
        {
            using (SystemDateTime.PauseTime())
            {
                _peerStateRepository.UpdateNonAckMessageCount(new PeerId("PeerId"), 10);
                _peerStateRepository.Save().Wait();

                DataContext.PeerStates.Execute().ShouldNotBeEmpty();

                _peerStateRepository.Purge(new PeerId("PeerId")).Wait();

                DataContext.PeerStates.Execute().ShouldBeEmpty();
            }
        }

        [Test]
        public void should_publish_non_acked_message_count_to_zero_when_peer_has_been_purged()
        {
            _peerStateRepository.UpdateNonAckMessageCount(new PeerId("PeerId"), 10);
            _peerStateRepository.Purge(new PeerId("PeerId"));

            _bus.Expect(new NonAckMessagesCountChanged(new[] { new NonAckMessage("PeerId", 0), }));
        }

        [Test]
        public void should_publish_non_acked_message_count_when_required()
        {
            _peerStateRepository.UpdateNonAckMessageCount(new PeerId("PeerId"), 10);
            _peerStateRepository.Handle(new PublishNonAckMessagesCountCommand());

            _bus.Expect(new NonAckMessagesCountChanged(new[] { new NonAckMessage("PeerId", 10), }));
        }

        [Test]
        public void should_publish_non_acked_message_count_only_for_the_peers_that_actually_changed_since_the_last_publication()
        {
            using (SystemDateTime.PauseTime())
            {
                _peerStateRepository.UpdateNonAckMessageCount(new PeerId("PeerId.1"), 10);
                _peerStateRepository.UpdateNonAckMessageCount(new PeerId("PeerId.2"), 20);

                SystemDateTime.Set(utcNow: SystemDateTime.UtcNow.Add(1.Second()));

                _peerStateRepository.Handle(new PublishNonAckMessagesCountCommand());

                _bus.ClearMessages();

                SystemDateTime.Set(utcNow: SystemDateTime.UtcNow.Add(1.Second()));

                _peerStateRepository.UpdateNonAckMessageCount(new PeerId("PeerId.1"), 2);
                _peerStateRepository.Handle(new PublishNonAckMessagesCountCommand());

                _bus.Expect(new NonAckMessagesCountChanged(new[] { new NonAckMessage("PeerId.1", 12), }));
            }
        }

        [Test]
        public void should_delete_all_buckets_for_peer_when_purged()
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

            _peerStateRepository.Purge(new PeerId("PeerId"));

            Wait.Until(() => !DataContext.PersistentMessages.Execute().Any(), 1.Second());
        }
    }
}