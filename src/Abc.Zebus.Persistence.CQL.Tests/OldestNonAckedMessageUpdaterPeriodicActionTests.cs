using System;
using System.Linq;
using Abc.Zebus.Persistence.CQL.Data;
using Abc.Zebus.Persistence.CQL.PeriodicAction;
using Abc.Zebus.Persistence.CQL.Storage;
using Abc.Zebus.Persistence.CQL.Testing;
using Abc.Zebus.Persistence.CQL.Tests.Cql;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.CQL.Tests
{
    public class OldestNonAckedMessageUpdaterPeriodicActionTests : CqlTestFixture<PersistenceCqlDataContext, ICqlPersistenceConfiguration>
    {
        private FakePeerStateRepository _peerStateRepo;
        private TestBus _testBus;
        private OldestNonAckedMessageUpdaterPeriodicAction _oldestMessageUpdater;

        public override void CreateSchema()
        {
            IgnoreWhenSet("APPVEYOR");
            IgnoreWhenSet("AZURE_PIPELINES");
            base.CreateSchema();
        }

        private void IgnoreWhenSet(string environmentVariable)
        {
            var env = Environment.GetEnvironmentVariable(environmentVariable);
            if (!string.IsNullOrEmpty(env) && bool.TryParse(env, out var isSet) && isSet)
                Assert.Ignore("We need a cassandra node for this");
        }

        [SetUp]
        public void SetUp()
        {
            _peerStateRepo = new FakePeerStateRepository();
            _testBus = new TestBus();
            _oldestMessageUpdater = new OldestNonAckedMessageUpdaterPeriodicAction(_testBus, _peerStateRepo, new Mock<ICqlPersistenceConfiguration>().Object, new CqlStorage(DataContext, _peerStateRepo, null, null));
        }

        [Test]
        public void should_update_oldest_non_acked_message_timestamp()
        {
            var peerId = new PeerId("PeerId");
            var now = SystemDateTime.UtcNow;
            InsertPersistentMessage(peerId, now.AddMilliseconds(1), x => x.IsAcked = true);
            InsertPersistentMessage(peerId, now.AddMilliseconds(2), x => x.IsAcked = false);
            InsertPersistentMessage(peerId, now.AddMilliseconds(3), x => x.IsAcked = false);
            InsertPersistentMessage(peerId, now.AddMilliseconds(4), x => x.IsAcked = true);
            InsertPersistentMessage(peerId, now.AddMilliseconds(5), x => x.IsAcked = false);
            _peerStateRepo.Add(new PeerState(peerId, 0, now.AddMilliseconds(1).Ticks));

            _oldestMessageUpdater.DoPeriodicAction();

            _peerStateRepo[peerId].OldestNonAckedMessageTimestampInTicks.ShouldEqual(now.AddMilliseconds(2).Ticks);
        }

        [Test]
        public void should_not_update_oldest_non_acked_message_timestamp_if_it_did_not_change()
        {
            var peerId = new PeerId("PeerId");
            var now = SystemDateTime.UtcNow;
            InsertPersistentMessage(peerId, now.AddMilliseconds(1), x => x.IsAcked = true);
            InsertPersistentMessage(peerId, now.AddMilliseconds(2), x => x.IsAcked = false);
            InsertPersistentMessage(peerId, now.AddMilliseconds(3), x => x.IsAcked = false);
            InsertPersistentMessage(peerId, now.AddMilliseconds(4), x => x.IsAcked = true);
            InsertPersistentMessage(peerId, now.AddMilliseconds(5), x => x.IsAcked = false);
            _peerStateRepo.Add(new PeerState(peerId, 0, now.AddMilliseconds(2).Ticks));

            _oldestMessageUpdater.DoPeriodicAction();

            _peerStateRepo[peerId].OldestNonAckedMessageTimestampInTicks.ShouldEqual(now.AddMilliseconds(2).Ticks);
        }

        [Test]
        public void should_take_last_message_timestamp_plus_one_tick_as_oldest_non_acked_message_when_all_messages_are_acked()
        {
            var peerId = new PeerId("PeerId");
            var now = SystemDateTime.UtcNow;
            InsertPersistentMessage(peerId, now.AddMilliseconds(1), x => x.IsAcked = true);
            InsertPersistentMessage(peerId, now.AddMilliseconds(2), x => x.IsAcked = true);
            InsertPersistentMessage(peerId, now.AddMilliseconds(3), x => x.IsAcked = true);
            InsertPersistentMessage(peerId, now.AddMilliseconds(4), x => x.IsAcked = true);
            InsertPersistentMessage(peerId, now.AddMilliseconds(5), x => x.IsAcked = true);
            _peerStateRepo.Add(new PeerState(peerId, 0, now.AddMilliseconds(2).Ticks));

            _oldestMessageUpdater.DoPeriodicAction();

            _peerStateRepo[peerId].OldestNonAckedMessageTimestampInTicks.ShouldEqual(now.AddMilliseconds(5).Ticks + 1);
        }

        [Test]
        public void should_take_utc_now_timestamp_as_oldest_non_acked_message_when_no_messages_are_acked()
        {
            using (SystemDateTime.PauseTime())
            {
                var peerId = new PeerId("PeerId");
                var now = SystemDateTime.UtcNow;
                _peerStateRepo.Add(new PeerState(peerId, 0, now.AddMilliseconds(2).Ticks));

                _oldestMessageUpdater.DoPeriodicAction();

                _peerStateRepo[peerId].OldestNonAckedMessageTimestampInTicks.ShouldEqual(now.Ticks);
            }
        }

        [Test]
        public void should_delete_buckets_when_all_messages_are_acked_in_it()
        {
            var peerId = new PeerId("PeerId");
            var now = DateTime.UtcNow.Date;
            _peerStateRepo.Add(new PeerState(peerId, 0, now.AddHours(-5).Ticks));

            // first bucket - 3 hours ago - all acked
            InsertPersistentMessage(peerId, now.AddHours(-3), x => x.IsAcked = true);
            InsertPersistentMessage(peerId, now.AddHours(-3).AddMinutes(1), x => x.IsAcked = true);
            InsertPersistentMessage(peerId, now.AddHours(-3).AddMinutes(2), x => x.IsAcked = true);
            // second bucket - 2 hours ago - all acked
            InsertPersistentMessage(peerId, now.AddHours(-2), x => x.IsAcked = true);
            InsertPersistentMessage(peerId, now.AddHours(-2).AddMinutes(1), x => x.IsAcked = true);
            InsertPersistentMessage(peerId, now.AddHours(-2).AddMinutes(2), x => x.IsAcked = true);
            // third bucket - 1 hours ago - with non acked
            InsertPersistentMessage(peerId, now.AddHours(-1), x => x.IsAcked = true);
            InsertPersistentMessage(peerId, now.AddHours(-1).AddMinutes(1), x => x.IsAcked = false); // <--- non acked !
            InsertPersistentMessage(peerId, now.AddHours(-1).AddMinutes(2), x => x.IsAcked = true);
            // current bucket - all acked
            InsertPersistentMessage(peerId, now, x => x.IsAcked = true);

            var messages = DataContext.PersistentMessages.Execute().ToList();
            messages.Count.ShouldEqual(10);

            _oldestMessageUpdater.DoPeriodicAction();

            Wait.Until(()=>DataContext.PersistentMessages.Execute().Count() == 4, 2.Seconds());

            _peerStateRepo[peerId].OldestNonAckedMessageTimestampInTicks.ShouldEqual(now.AddHours(-1).AddMinutes(1).Ticks);
            var persistentMessagesFromDatabase = DataContext.PersistentMessages.Execute().ToList();
            var storedMessages = persistentMessagesFromDatabase.Select(x => new { x.UniqueTimestampInTicks, x.IsAcked }).ToList();
            storedMessages.ShouldBeEquivalentTo(new[]
            {
                new { UniqueTimestampInTicks = now.AddHours(-1).Ticks, IsAcked = true },
                new { UniqueTimestampInTicks = now.AddHours(-1).AddMinutes(1).Ticks, IsAcked = false },
                new { UniqueTimestampInTicks = now.AddHours(-1).AddMinutes(2).Ticks, IsAcked = true },
                new { UniqueTimestampInTicks = now.Ticks, IsAcked = true },
            });
        }

        [Test]
        public void should_save_peer_state_repo()
        {
            _oldestMessageUpdater.DoPeriodicAction();

            _peerStateRepo.HasBeenSaved.ShouldBeTrue();
        }

        private void InsertPersistentMessage(PeerId peerId, DateTime timestamp, Action<PersistentMessage> updateMessage = null)
        {
            var message = new PersistentMessage
            {
                PeerId = peerId.ToString(),
                BucketId = BucketIdHelper.GetBucketId(timestamp),
                IsAcked = true,
                UniqueTimestampInTicks = timestamp.Ticks,
                TransportMessage = new byte[0]
            };
            updateMessage?.Invoke(message);
            DataContext.PersistentMessages.Insert(message).Execute();
        }
    }
}
