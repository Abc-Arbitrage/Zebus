using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.CQL.Data;
using Abc.Zebus.Persistence.CQL.Storage;
using Abc.Zebus.Persistence.CQL.Tests.Cql;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Persistence.Reporter;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using Cassandra.Data.Linq;
using Moq;
using NUnit.Framework;
using ProtoBuf;

namespace Abc.Zebus.Persistence.CQL.Tests
{
    public class CqlStorageTests : CqlTestFixture<PersistenceCqlDataContext, ICqlPersistenceConfiguration>
    {
        private static readonly long _expectedOldestNonAckedMessageTimestampSafetyOffset = new TimeSpan(00, 15, 00).Ticks;

        private CqlStorage _storage;
        private Mock<IPersistenceConfiguration> _configurationMock;
        private Mock<IReporter> _reporterMock;

        public override void CreateSchema()
        {
            IgnoreWhenSet("TF_BUILD");
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
            _configurationMock = new Mock<IPersistenceConfiguration>();
            _reporterMock = new Mock<IReporter>();
            _storage = new CqlStorage(DataContext, _configurationMock.Object, _reporterMock.Object);
            _storage.Start();
        }

        [TearDown]
        public void Teardown()
        {
            _storage.Dispose();
        }

        [Test]
        public void should_initialize_peer_state_on_start()
        {
            DataContext.PeerStates.Insert(new CassandraPeerState(new PeerState(new PeerId("New")))).Execute();

            var storage = new CqlStorage(DataContext, _configurationMock.Object, _reporterMock.Object);
            storage.Start();

            storage.GetAllKnownPeers().Count().ShouldEqual(1);
        }

        [Test]
        public async Task should_write_message_entry_fields_to_cassandra()
        {
            using (SystemDateTime.PauseTime())
            {
                var messageBytes = new byte[512];
                new Random().NextBytes(messageBytes);
                var messageId = MessageId.NextId();
                var peerId = "Abc.Peer.0";

                await _storage.Write(new List<MatcherEntry> { MatcherEntry.Message(new PeerId(peerId), messageId, MessageTypeId.PersistenceStopping, messageBytes) });

                var retrievedMessage = DataContext.PersistentMessages.Execute().ExpectedSingle();
                retrievedMessage.TransportMessage.ShouldBeEquivalentTo(messageBytes, true);
                retrievedMessage.BucketId.ShouldEqual(GetBucketIdFromMessageId(messageId));
                retrievedMessage.IsAcked.ShouldBeFalse();
                retrievedMessage.PeerId.ShouldEqual(peerId);
                retrievedMessage.UniqueTimestampInTicks.ShouldEqual(messageId.GetDateTime().Ticks);
                var writeTimeRow = DataContext.Session.Execute("SELECT WRITETIME(\"IsAcked\") FROM \"PersistentMessage\";").ExpectedSingle();
                writeTimeRow.GetValue<long>(0).ShouldEqual(ToUnixMicroSeconds(messageId.GetDateTime()));

                var peerState = DataContext.PeerStates.Execute().ExpectedSingle();
                peerState.NonAckedMessageCount.ShouldEqual(1);
                peerState.PeerId.ShouldEqual(peerId);
                peerState.OldestNonAckedMessageTimestamp.ShouldEqual(messageId.GetDateTime().Ticks - PeerState.MessagesTimeToLive.Ticks);
            }
        }

        [Test]
        public async Task should_write_ZebusV2_message_entry_fields_to_cassandra()
        {
            var messageBytes = new byte[512];
            new Random().NextBytes(messageBytes);
            var timestamp = DateTime.UtcNow;
            var messageId = new MessageId(MessageIdV2.CreateNewSequentialId(timestamp.Ticks));
            var peerId = "Abc.Peer.0";

            await _storage.Write(new List<MatcherEntry> { MatcherEntry.Message(new PeerId(peerId), messageId, MessageTypeId.PersistenceStopping, messageBytes) });

            var retrievedMessage = DataContext.PersistentMessages.Execute().ExpectedSingle();
            retrievedMessage.TransportMessage.ShouldBeEquivalentTo(messageBytes, true);
            retrievedMessage.BucketId.ShouldEqual(GetBucketIdFromDateTime(timestamp));
            retrievedMessage.IsAcked.ShouldBeFalse();
            retrievedMessage.PeerId.ShouldEqual(peerId);
            retrievedMessage.UniqueTimestampInTicks.ShouldEqual(timestamp.Ticks);
            var writeTimeRow = DataContext.Session.Execute("SELECT WRITETIME(\"IsAcked\") FROM \"PersistentMessage\";").ExpectedSingle();
            writeTimeRow.GetValue<long>(0).ShouldEqual(ToUnixMicroSeconds(timestamp));
        }

        [Test]
        public async Task should_not_overwrite_messages_with_same_time_component_and_different_message_id()
        {
            var messageBytes = new byte[512];
            new Random().NextBytes(messageBytes);
            var messageId = new MessageId(Guid.Parse("0000c399-1ab0-e511-9706-ae1ea5dcf365"));      // Time component @2016-01-01 00:00:00Z
            var otherMessageId = new MessageId(Guid.Parse("0000c399-1ab0-e511-9806-f1ef55aac8e9")); // Time component @2016-01-01 00:00:00Z
            var peerId = "Abc.Peer.0";

            await _storage.Write(new List<MatcherEntry>
            {
                MatcherEntry.Message(new PeerId(peerId), messageId, MessageTypeId.PersistenceStopping, messageBytes),
                MatcherEntry.Message(new PeerId(peerId), otherMessageId, MessageTypeId.PersistenceStopping, messageBytes),
            });

            var retrievedMessages = DataContext.PersistentMessages.Execute().ToList();
            retrievedMessages.Count.ShouldEqual(2);
        }

        [Test]
        public async Task should_write_ack_entry_fields_to_cassandra()
        {
            var messageId = MessageId.NextId();
            var peerId = "Abc.Peer.0";

            await _storage.Write(new List<MatcherEntry> { MatcherEntry.Ack(new PeerId(peerId), messageId) });

            var retrievedMessage = DataContext.PersistentMessages.Execute().ExpectedSingle();
            retrievedMessage.TransportMessage.ShouldBeNull();
            retrievedMessage.BucketId.ShouldEqual(GetBucketIdFromMessageId(messageId));
            retrievedMessage.IsAcked.ShouldBeTrue();
            retrievedMessage.PeerId.ShouldEqual(peerId);
            retrievedMessage.UniqueTimestampInTicks.ShouldEqual(messageId.GetDateTime().Ticks);
            var writeTimeRow = DataContext.Session.Execute("SELECT WRITETIME(\"IsAcked\") FROM \"PersistentMessage\";").ExpectedSingle();
            writeTimeRow.GetValue<long>(0).ShouldEqual(ToUnixMicroSeconds(messageId.GetDateTime()) + 1);
        }

        [Test]
        public async Task should_write_ZebusV2_ack_entry_fields_to_cassandra()
        {
            var timestamp = DateTime.UtcNow;
            var messageId = new MessageId(MessageIdV2.CreateNewSequentialId(timestamp.Ticks));
            var peerId = "Abc.Peer.0";

            await _storage.Write(new List<MatcherEntry> { MatcherEntry.Ack(new PeerId(peerId), messageId) });

            var retrievedMessage = DataContext.PersistentMessages.Execute().ExpectedSingle();
            retrievedMessage.TransportMessage.ShouldBeNull();
            retrievedMessage.BucketId.ShouldEqual(GetBucketIdFromDateTime(timestamp));
            retrievedMessage.IsAcked.ShouldBeTrue();
            retrievedMessage.PeerId.ShouldEqual(peerId);
            retrievedMessage.UniqueTimestampInTicks.ShouldEqual(timestamp.Ticks);
            var writeTimeRow = DataContext.Session.Execute("SELECT WRITETIME(\"IsAcked\") FROM \"PersistentMessage\";").ExpectedSingle();
            writeTimeRow.GetValue<long>(0).ShouldEqual(ToUnixMicroSeconds(timestamp) + 1);
        }

        [Test]
        public async Task should_support_out_of_order_acks_and_messages()
        {
            var messageBytes = new byte[512];
            new Random().NextBytes(messageBytes);
            var messageId = MessageId.NextId();
            var peerId = "Abc.Peer.0";

            await _storage.Write(new List<MatcherEntry> { MatcherEntry.Ack(new PeerId(peerId), messageId) });
            await Task.Delay(50);
            await _storage.Write(new List<MatcherEntry> { MatcherEntry.Message(new PeerId(peerId), messageId, MessageTypeId.PersistenceStopping, messageBytes) });

            var retrievedMessage = DataContext.PersistentMessages.Execute().ExpectedSingle();
            retrievedMessage.TransportMessage.ShouldBeNull();
            retrievedMessage.BucketId.ShouldEqual(GetBucketIdFromMessageId(messageId));
            retrievedMessage.IsAcked.ShouldBeTrue();
            retrievedMessage.PeerId.ShouldEqual(peerId);
            retrievedMessage.UniqueTimestampInTicks.ShouldEqual(messageId.GetDateTime().Ticks);
        }

        [Test]
        public async Task should_support_out_of_order_ZebusV2_acks_and_messages()
        {
            var messageBytes = new byte[512];
            new Random().NextBytes(messageBytes);
            var timestamp = DateTime.UtcNow;
            var messageId = new MessageId(MessageIdV2.CreateNewSequentialId(timestamp.Ticks));
            var peerId = "Abc.Peer.0";

            await _storage.Write(new List<MatcherEntry> { MatcherEntry.Ack(new PeerId(peerId), messageId) });
            await Task.Delay(50);
            await _storage.Write(new List<MatcherEntry> { MatcherEntry.Message(new PeerId(peerId), messageId, MessageTypeId.PersistenceStopping, messageBytes) });

            var retrievedMessage = DataContext.PersistentMessages.Execute().ExpectedSingle();
            retrievedMessage.TransportMessage.ShouldBeNull();
            retrievedMessage.BucketId.ShouldEqual(GetBucketIdFromDateTime(timestamp));
            retrievedMessage.IsAcked.ShouldBeTrue();
            retrievedMessage.PeerId.ShouldEqual(peerId);
            retrievedMessage.UniqueTimestampInTicks.ShouldEqual(timestamp.Ticks);
        }

        [Test]
        public async Task should_remove_from_cassandra_when_asked_to_remove_peer()
        {
            var peerId = new PeerId("PeerId");
            await _storage.Write(new List<MatcherEntry> { MatcherEntry.Message(peerId, MessageId.NextId(), MessageTypeId.PersistenceStopping, new byte[0]) });

            await _storage.RemovePeer(peerId);

            DataContext.PeerStates.Execute().ShouldBeEmpty();
        }

        [Test]
        public async Task should_delete_all_buckets_for_peer_when_removed()
        {
            var peerId = new PeerId("PeerId");
            await _storage.Write(new List<MatcherEntry> { MatcherEntry.Message(peerId, MessageId.NextId(), MessageTypeId.PersistenceStopping, new byte[0]) });

            DataContext.PersistentMessages.Execute().Count().ShouldEqual(1);

            await _storage.RemovePeer(new PeerId("PeerId"));

            DataContext.PersistentMessages.Execute().Any().ShouldBeFalse();
        }

        [Test]
        public async Task should_return_cql_message_reader()
        {
            var peerId = new PeerId("PeerId");
            await _storage.Write(new List<MatcherEntry> { MatcherEntry.Message(peerId, MessageId.NextId(), MessageTypeId.PersistenceStopping, new byte[0]) });

            _storage.CreateMessageReader(peerId).ShouldNotBeNull();
        }

        [Test]
        public void should_return_null_when_asked_for_a_message_reader_for_an_unknown_peer_id()
        {
            _storage.CreateMessageReader(new PeerId("UnknownPeerId")).ShouldBeNull();
        }

        [Test]
        public async Task should_store_messages_in_different_buckets()
        {
            MessageId.ResetLastTimestamp();

            var firstTime = DateTime.Now;
            using (SystemDateTime.Set(firstTime))
            using (MessageId.PauseIdGenerationAtDate(firstTime))
            {
                var peerId = new PeerId("Abc.Testing.Target");

                var firstMessageId = MessageId.NextId();
                await _storage.Write(new[] { MatcherEntry.Message(peerId, firstMessageId, new MessageTypeId("Abc.Message"), new byte[] { 0x01, 0x02, 0x03 }) });

                var secondTime = firstTime.AddHours(1);
                SystemDateTime.Set(secondTime);
                MessageId.PauseIdGenerationAtDate(secondTime);

                var secondMessageId = MessageId.NextId();
                await _storage.Write(new[] { MatcherEntry.Message(peerId, secondMessageId, new MessageTypeId("Abc.OtherMessage"), new byte[] { 0x04, 0x05, 0x06 }) });

                var persistedMessages = DataContext.PersistentMessages.Execute().OrderBy(x => x.UniqueTimestampInTicks).ToList(); // Results are only ordered withing a partition
                persistedMessages.Count.ShouldEqual(2);

                persistedMessages.First().ShouldHaveSamePropertiesAs(new PersistentMessage
                {
                    BucketId = BucketIdHelper.GetBucketId(firstTime),
                    PeerId = peerId.ToString(),
                    IsAcked = false,
                    MessageId = firstMessageId.Value,
                    TransportMessage = new byte[] { 0x01, 0x02, 0x03 },
                    UniqueTimestampInTicks = firstTime.Ticks
                });
                persistedMessages.Last().ShouldHaveSamePropertiesAs(new PersistentMessage
                {
                    BucketId = BucketIdHelper.GetBucketId(secondTime),
                    PeerId = peerId.ToString(),
                    IsAcked = false,
                    MessageId = secondMessageId.Value,
                    TransportMessage = new byte[] { 0x04, 0x05, 0x06 },
                    UniqueTimestampInTicks = secondTime.Ticks
                });
            }
        }

        [Test]
        public async Task should_update_non_ack_message_count()
        {
            var firstPeer = new PeerId("Abc.Testing.Target");
            var secondPeer = new PeerId("Abc.Testing.OtherTarget");

            await _storage.Write(new[] { MatcherEntry.Message(firstPeer, MessageId.NextId(), new MessageTypeId("Abc.Message"), new byte[] { 0x01, 0x02, 0x03 }) });
            await _storage.Write(new[] { MatcherEntry.Message(secondPeer, MessageId.NextId(), new MessageTypeId("Abc.Message"), new byte[] { 0x04, 0x05, 0x06 }) });
            await _storage.Write(new[] { MatcherEntry.Message(firstPeer, MessageId.NextId(), new MessageTypeId("Abc.Message"), new byte[] { 0x07, 0x08, 0x09 }) });

            var nonAckedMessageCountsForUpdatedPeers = _storage.GetNonAckedMessageCounts();
            nonAckedMessageCountsForUpdatedPeers[firstPeer].ShouldEqual(2);
            nonAckedMessageCountsForUpdatedPeers[secondPeer].ShouldEqual(1);

            await _storage.Write(new[] { MatcherEntry.Ack(firstPeer, MessageId.NextId()) });

            nonAckedMessageCountsForUpdatedPeers = _storage.GetNonAckedMessageCounts();
            nonAckedMessageCountsForUpdatedPeers[firstPeer].ShouldEqual(1);
            nonAckedMessageCountsForUpdatedPeers[secondPeer].ShouldEqual(1);
        }

        [Test]
        public async Task should_persist_messages_in_order()
        {
            var firstPeer = new PeerId("Abc.Testing.Target");
            var secondPeer = new PeerId("Abc.Testing.OtherTarget");

            using (MessageId.PauseIdGeneration())
            using (SystemDateTime.PauseTime())
            {
                var transportMessages = Enumerable.Range(1, 100).Select(CreateTestTransportMessage).ToList();
                var messages = transportMessages.SelectMany(x =>
                                                        {
                                                            var transportMessageBytes = Serialization.Serializer.Serialize(x).ToArray();
                                                            return new[]
                                                            {
                                                                MatcherEntry.Message(firstPeer, x.Id, x.MessageTypeId, transportMessageBytes),
                                                                MatcherEntry.Message(secondPeer, x.Id, x.MessageTypeId, transportMessageBytes),
                                                            };
                                                        })
                                                        .ToList();

                await _storage.Write(messages);

                var nonAckedMessageCountsForUpdatedPeers = _storage.GetNonAckedMessageCounts();
                nonAckedMessageCountsForUpdatedPeers[firstPeer].ShouldEqual(100);
                nonAckedMessageCountsForUpdatedPeers[secondPeer].ShouldEqual(100);

                var readerForFirstPeer = (CqlMessageReader)_storage.CreateMessageReader(firstPeer);
                var expectedTransportMessages = transportMessages.Select(Serialization.Serializer.Serialize).Select(x => x.ToArray()).ToList();
                readerForFirstPeer.GetUnackedMessages().ToList().ShouldEqualDeeply(expectedTransportMessages);

                var readerForSecondPeer = (CqlMessageReader)_storage.CreateMessageReader(secondPeer);
                readerForSecondPeer.GetUnackedMessages().ToList().ShouldEqualDeeply(expectedTransportMessages);
            }
        }

        [Test]
        public void should_report_storage_informations()
        {
            var peer = new PeerId("peer");

            _storage.Write(new[]
            {
                MatcherEntry.Message(peer, MessageId.NextId(), new MessageTypeId("Abc.Message"), new byte[] { 0x01, 0x02, 0x03 }),
                MatcherEntry.Message(peer, MessageId.NextId(), new MessageTypeId("Abc.Message.Fat"), new byte[] { 0x01, 0x02, 0x03, 0x04 }),
            });

            _reporterMock.Verify(r => r.AddStorageReport(2, 7, 4, "Abc.Message.Fat"));
        }

        [Test]
        public async Task should_update_oldest_non_acked_message_timestamp()
        {
            using (SystemDateTime.PauseTime())
            {
                var peerId = new PeerId("PeerId");
                var now = SystemDateTime.UtcNow;
                InsertPersistentMessage(peerId, now.AddMilliseconds(1));
                InsertPersistentMessage(peerId, now.AddMilliseconds(2), AckState.Unacked);
                InsertPersistentMessage(peerId, now.AddMilliseconds(3), AckState.Unacked);
                InsertPersistentMessage(peerId, now.AddMilliseconds(4));
                InsertPersistentMessage(peerId, now.AddMilliseconds(5), AckState.Unacked);
                var peerState = new PeerState(peerId, 0, now.AddMinutes(-30).Ticks);
                InsertPeerState(peerState);

                await _storage.UpdateNewOldestMessageTimestamp(peerState);

                GetPeerState(peerId).OldestNonAckedMessageTimestampInTicks.ShouldEqual(now.AddMilliseconds(2).Ticks - _expectedOldestNonAckedMessageTimestampSafetyOffset);
            }
        }

        [Test]
        public async Task should_not_update_oldest_non_acked_message_timestamp_if_it_did_not_change()
        {
            var peerId = new PeerId("PeerId");
            var now = SystemDateTime.UtcNow;
            InsertPersistentMessage(peerId, now.AddMilliseconds(1));
            InsertPersistentMessage(peerId, now.AddMilliseconds(2), AckState.Unacked);
            InsertPersistentMessage(peerId, now.AddMilliseconds(3), AckState.Unacked);
            InsertPersistentMessage(peerId, now.AddMilliseconds(4));
            InsertPersistentMessage(peerId, now.AddMilliseconds(5), AckState.Unacked);
            var peerState = new PeerState(peerId, 0, now.AddMilliseconds(2).Ticks - _expectedOldestNonAckedMessageTimestampSafetyOffset);
            InsertPeerState(peerState);

            await _storage.UpdateNewOldestMessageTimestamp(peerState);

            GetPeerState(peerId).OldestNonAckedMessageTimestampInTicks.ShouldEqual(now.AddMilliseconds(2).Ticks - _expectedOldestNonAckedMessageTimestampSafetyOffset);
        }

        [Test]
        public async Task should_take_last_message_timestamp_minus_safety_offset_as_oldest_non_acked_message_when_all_messages_are_acked()
        {
            var peerId = new PeerId("PeerId");
            var now = SystemDateTime.UtcNow;

            InsertPersistentMessage(peerId, now.AddMilliseconds(1));
            InsertPersistentMessage(peerId, now.AddMilliseconds(2));
            InsertPersistentMessage(peerId, now.AddMilliseconds(3));
            InsertPersistentMessage(peerId, now.AddMilliseconds(4));
            InsertPersistentMessage(peerId, now.AddMilliseconds(5));

            var peerState = new PeerState(peerId, 0, now.AddHours(-1).Ticks);
            InsertPeerState(peerState);

            await _storage.UpdateNewOldestMessageTimestamp(peerState);

            GetPeerState(peerId).OldestNonAckedMessageTimestampInTicks.ShouldEqual(now.AddMilliseconds(5).Ticks - _expectedOldestNonAckedMessageTimestampSafetyOffset);
        }

        [Test]
        public async Task should_take_utc_now_timestamp_as_oldest_non_acked_message_when_no_messages_are_acked()
        {
            using (SystemDateTime.PauseTime())
            {
                var peerId = new PeerId("PeerId");
                var now = SystemDateTime.UtcNow;
                var peerState = new PeerState(peerId, 0, now.AddDays(-2).Ticks);
                InsertPeerState(peerState);

                await _storage.UpdateNewOldestMessageTimestamp(peerState);

                GetPeerState(peerId).OldestNonAckedMessageTimestampInTicks.ShouldEqual(now.Ticks - _expectedOldestNonAckedMessageTimestampSafetyOffset);
            }
        }

        [Test]
        public async Task should_delete_buckets_when_all_messages_are_acked_in_it()
        {
            var peerId = new PeerId("PeerId");
            var now = DateTime.UtcNow.Date.AddDays(-1).AddHours(15).AddMinutes(30);
            var peerState = new PeerState(peerId, 0, now.AddHours(-5).Ticks);
            InsertPeerState(peerState);

            // first bucket - 3 hours ago - all acked
            InsertPersistentMessage(peerId, now.AddHours(-3));
            InsertPersistentMessage(peerId, now.AddHours(-3).AddMinutes(1));
            InsertPersistentMessage(peerId, now.AddHours(-3).AddMinutes(2));
            // second bucket - 2 hours ago - all acked
            InsertPersistentMessage(peerId, now.AddHours(-2));
            InsertPersistentMessage(peerId, now.AddHours(-2).AddMinutes(1));
            InsertPersistentMessage(peerId, now.AddHours(-2).AddMinutes(2));
            // third bucket - 1 hours ago - with non acked
            InsertPersistentMessage(peerId, now.AddHours(-1));
            InsertPersistentMessage(peerId, now.AddHours(-1).AddMinutes(1), AckState.Unacked);
            InsertPersistentMessage(peerId, now.AddHours(-1).AddMinutes(2));
            // current bucket - all acked
            InsertPersistentMessage(peerId, now);

            DataContext.PersistentMessages.Execute().Count().ShouldEqual(10);

            await _storage.UpdateNewOldestMessageTimestamp(peerState);
            await _storage.CleanBucketTask;

            DataContext.PersistentMessages.Execute().Count().ShouldEqual(4);

            GetPeerState(peerId).OldestNonAckedMessageTimestampInTicks.ShouldEqual(now.AddHours(-1).AddMinutes(1).Ticks - _expectedOldestNonAckedMessageTimestampSafetyOffset);
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
        public async Task should_delete_one_bucket_when_all_messages_are_acked_in_it()
        {
            var peerId = new PeerId("PeerId");
            var now = DateTime.UtcNow.Date.AddDays(-1).AddHours(15).AddMinutes(30);
            var peerState = new PeerState(peerId, 0, now.AddHours(-1).Ticks);
            InsertPeerState(peerState);

            // first bucket - 1 hours ago - all acked
            InsertPersistentMessage(peerId, now.AddHours(-1));
            InsertPersistentMessage(peerId, now.AddHours(-1).AddMinutes(1));
            InsertPersistentMessage(peerId, now.AddHours(-1).AddMinutes(2));
            // current bucket
            InsertPersistentMessage(peerId, now, AckState.Unacked);

            DataContext.PersistentMessages.Execute().Count().ShouldEqual(4);

            await _storage.UpdateNewOldestMessageTimestamp(peerState);
            await _storage.CleanBucketTask;

            DataContext.PersistentMessages.Execute().Count().ShouldEqual(1);
        }

        private static long GetBucketIdFromMessageId(MessageId message)
        {
            return GetBucketIdFromDateTime(message.GetDateTime());
        }

        private static long GetBucketIdFromDateTime(DateTime timestamp)
        {
            return new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, 0, 0).Ticks;
        }

        private void InsertPersistentMessage(PeerId peerId, DateTime timestamp, AckState ackState = AckState.Acked)
        {
            var message = new PersistentMessage
            {
                PeerId = peerId.ToString(),
                BucketId = BucketIdHelper.GetBucketId(timestamp),
                IsAcked = ackState == AckState.Acked,
                UniqueTimestampInTicks = timestamp.Ticks,
                TransportMessage = new byte[0]
            };
            DataContext.PersistentMessages.Insert(message).Execute();
        }

        private void InsertPeerState(PeerState peerState)
        {
            DataContext.PeerStates.Insert(new CassandraPeerState(peerState)).Execute();
        }

        private PeerState GetPeerState(in PeerId peerId)
        {
            var peerString = peerId.ToString();
            var state = DataContext.PeerStates.Where(x => x.PeerId == peerString).Execute().Single();
            return new PeerState(new PeerId(state.PeerId), state.NonAckedMessageCount, state.OldestNonAckedMessageTimestamp);
        }

        private static TransportMessage CreateTestTransportMessage(int i)
        {
            MessageId.PauseIdGenerationAtDate(SystemDateTime.UtcNow.Date.AddSeconds(i * 10));
            return new Message1(i).ToTransportMessage();
        }

        private static long ToUnixMicroSeconds(DateTime timestamp)
        {
            var origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            var diff = timestamp - origin;
            var diffInMicroSeconds = diff.Ticks / 10;
            return diffInMicroSeconds;
        }

        [ProtoContract]
        private class Message1 : IEvent
        {
            [ProtoMember(1, IsRequired = true)]
            public int Id { get; private set; }

            public Message1(int id)
            {
                Id = id;
            }
        }

        private enum AckState
        {
            Acked,
            Unacked,
        }
    }
}
