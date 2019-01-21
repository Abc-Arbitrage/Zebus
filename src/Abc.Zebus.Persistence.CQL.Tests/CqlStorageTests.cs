using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.CQL.Data;
using Abc.Zebus.Persistence.CQL.Storage;
using Abc.Zebus.Persistence.CQL.Testing;
using Abc.Zebus.Persistence.CQL.Tests.Cql;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Persistence.Reporter;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using Moq;
using NUnit.Framework;
using ProtoBuf;

namespace Abc.Zebus.Persistence.CQL.Tests
{
    public class CqlStorageTests : CqlTestFixture<PersistenceCqlDataContext, ICqlPersistenceConfiguration>
    {
        private CqlStorage _storage;
        private FakePeerStateRepository _peerStateRepository;
        private Mock<IPersistenceConfiguration> _configurationMock;
        private Mock<IReporter> _reporterMock;

        public override void CreateSchema()
        {
            IgnoreWhenSet("APPVEYOR");
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
            _peerStateRepository = new FakePeerStateRepository();
            _storage = new CqlStorage(DataContext, _peerStateRepository, _configurationMock.Object, _reporterMock.Object);
            _storage.Start();
        }

        [TearDown]
        public void Teardown()
        {
            _storage.Dispose();
        }

        [Test]
        public void should_initialize_peer_state_repository_on_start()
        {
            var peerStateRepository = new FakePeerStateRepository();
            var storage = new CqlStorage(DataContext, peerStateRepository, _configurationMock.Object, _reporterMock.Object);
            storage.Start();

            peerStateRepository.IsInitialized.ShouldBeTrue();
        }

        [Test]
        public void should_save_peer_state_repository_on_stop()
        {
            var peerStateRepository = new FakePeerStateRepository();
            var storage = new CqlStorage(DataContext, peerStateRepository, _configurationMock.Object, _reporterMock.Object);
            storage.Start();
            storage.Stop();

            peerStateRepository.IsInitialized.ShouldBeTrue();
            peerStateRepository.HasBeenSaved.ShouldBeTrue();
        }

        [Test]
        public async Task should_write_message_entry_fields_to_cassandra()
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

        private static long ToUnixMicroSeconds(DateTime timestamp)
        {
            var origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            var diff = timestamp - origin;
            var diffInMicroSeconds = diff.Ticks / 10;
            return diffInMicroSeconds;
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
        public void should_call_peer_state_repository_when_asked_to_remove_peer()
        {
            var peerId = new PeerId("PeerId");
            _peerStateRepository.Add(new PeerState(peerId));
            var peerState =_peerStateRepository[peerId];

            _storage.RemovePeer(peerId);
            
            peerState.Removed.ShouldBeTrue();
            _peerStateRepository.GetPeerStateFor(peerId).ShouldBeNull();
        }

        [Test]
        public void should_return_cql_message_reader()
        {
            var peerId = new PeerId("PeerId");
            _peerStateRepository.Add(new PeerState(peerId));

            _storage.CreateMessageReader(peerId).ShouldNotBeNull();
        }

        [Test]
        public void should_return_null_when_asked_for_a_message_reader_for_an_unknown_peer_id()
        {
            _storage.CreateMessageReader(new PeerId("UnknownPeerId")).ShouldBeNull();
        }

        private static long GetBucketIdFromMessageId(MessageId message)
        {
            return GetBucketIdFromDateTime(message.GetDateTime());
        }

        private static long GetBucketIdFromDateTime(DateTime timestamp)
        {
            return new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, 0, 0).Ticks;
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
        public void should_update_non_ack_message_count()
        {
            var firstPeer = new PeerId("Abc.Testing.Target");
            var secondPeer = new PeerId("Abc.Testing.OtherTarget");
            
            _storage.Write(new[] { MatcherEntry.Message(firstPeer, MessageId.NextId(), new MessageTypeId("Abc.Message"), new byte[] { 0x01, 0x02, 0x03 }) });
            _storage.Write(new[] { MatcherEntry.Message(secondPeer, MessageId.NextId(), new MessageTypeId("Abc.Message"), new byte[] { 0x04, 0x05, 0x06 }) });
            _storage.Write(new[] { MatcherEntry.Message(firstPeer, MessageId.NextId(), new MessageTypeId("Abc.Message"), new byte[] { 0x07, 0x08, 0x09 }) });

            _peerStateRepository[firstPeer].NonAckedMessageCount.ShouldEqual(2);
            _peerStateRepository[secondPeer].NonAckedMessageCount.ShouldEqual(1);

            _storage.Write(new[] { MatcherEntry.Ack(firstPeer, MessageId.NextId()) });

            _peerStateRepository[firstPeer].NonAckedMessageCount.ShouldEqual(1);
            _peerStateRepository[secondPeer].NonAckedMessageCount.ShouldEqual(1);
        }

        [Test]
        public async Task should_persist_messages_in_order()
        {
            var firstPeer = new PeerId("Abc.Testing.Target");
            var secondPeer = new PeerId("Abc.Testing.OtherTarget");
            _peerStateRepository.Add(new PeerState(firstPeer, 0, SystemDateTime.UtcNow.Date.Ticks));
            _peerStateRepository.Add(new PeerState(secondPeer, 0, SystemDateTime.UtcNow.Date.Ticks));

            using (MessageId.PauseIdGeneration())
            using (SystemDateTime.PauseTime())
            {
                var expectedTransportMessages = Enumerable.Range(1, 100).Select(CreateTestTransportMessage).ToList();
                var messages = expectedTransportMessages.SelectMany(x =>
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

                _peerStateRepository[firstPeer].NonAckedMessageCount.ShouldEqual(100);
                _peerStateRepository[secondPeer].NonAckedMessageCount.ShouldEqual(100);

                var readerForFirstPeer = (CqlMessageReader)_storage.CreateMessageReader(firstPeer);
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

        private static TransportMessage CreateTestTransportMessage(int i)
        {
            MessageId.PauseIdGenerationAtDate(SystemDateTime.UtcNow.Date.AddSeconds(i * 10));
            return new Message1(i).ToTransportMessage();
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
    }
}
