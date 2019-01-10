using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Persistence.Reporter;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using Moq;
using NUnit.Framework;
using ProtoBuf;

namespace Abc.Zebus.Persistence.RocksDb.Tests
{
    public class RocksDbStorageTests
    {
        private RocksDbStorage _storage;
        private Mock<IReporter> _reporterMock;
        private string _dbName;

        [SetUp]
        public void SetUp()
        {
            _dbName = Guid.NewGuid().ToString();

            _reporterMock = new Mock<IReporter>();
            _storage = new RocksDbStorage(_dbName);
            _storage.Start();
        }

        [TearDown]
        public void Teardown()
        {
            _storage.Stop();
        }

        [Test]
        public async Task should_write_message_entry_fields_to_cassandra()
        {
            var inputMessage = CreateTestTransportMessage(1);
            var messageBytes = Serialization.Serializer.Serialize(inputMessage).ToArray();
            var messageId = MessageId.NextId();

            var peerId = new PeerId("Abc.Peer.0");
            await _storage.Write(new List<MatcherEntry> { MatcherEntry.Message(peerId, messageId, MessageTypeId.PersistenceStopping, messageBytes) });

            var messages = _storage.CreateMessageReader(peerId).GetUnackedMessages();
            var retrievedMessage = messages.Single();
            retrievedMessage.ShouldEqualDeeply(inputMessage);
        }

        [Test]
        public async Task should_not_overwrite_messages_with_same_time_component_and_different_message_id()
        {
            var messageBytes = Serialization.Serializer.Serialize(CreateTestTransportMessage(1)).ToArray();
            var messageId = new MessageId(Guid.Parse("0000c399-1ab0-e511-9706-ae1ea5dcf365"));      // Time component @2016-01-01 00:00:00Z
            var otherMessageId = new MessageId(Guid.Parse("0000c399-1ab0-e511-9806-f1ef55aac8e9")); // Time component @2016-01-01 00:00:00Z

            var peerId = new PeerId("Abc.Peer.0");
            await _storage.Write(new List<MatcherEntry>
            {
                MatcherEntry.Message(peerId, messageId, MessageTypeId.PersistenceStopping, messageBytes),
                MatcherEntry.Message(peerId, otherMessageId, MessageTypeId.PersistenceStopping, messageBytes),
            });

            var messages = _storage.CreateMessageReader(peerId).GetUnackedMessages();
            messages.ToList().Count.ShouldEqual(2);
        }

        [Test]
        public async Task should_support_out_of_order_acks_and_messages()
        {
            var inputMessage = CreateTestTransportMessage(1);
            var messageBytes = Serialization.Serializer.Serialize(inputMessage).ToArray();
            var messageId = MessageId.NextId();

            var peerId = new PeerId("Abc.Peer.0");
            await _storage.Write(new List<MatcherEntry> { MatcherEntry.Ack(peerId, messageId) });
            await Task.Delay(50);
            await _storage.Write(new List<MatcherEntry> { MatcherEntry.Message(peerId, messageId, MessageTypeId.PersistenceStopping, messageBytes) });

            var messageReader = _storage.CreateMessageReader(peerId);
            messageReader.ShouldNotBeNull();
            var messages = messageReader.GetUnackedMessages().ToList();
            messages.ShouldBeEmpty();
        }

        [Test]
        public void should_return_null_when_asked_for_a_message_reader_for_an_unknown_peer_id()
        {
            _storage.CreateMessageReader(new PeerId("UnknownPeerId")).ShouldBeNull();
        }

        [Test]
        public async Task should_remove_peer()
        {
            var inputMessage = CreateTestTransportMessage(1);
            var messageBytes = Serialization.Serializer.Serialize(inputMessage).ToArray();
            var messageId = MessageId.NextId();

            var peerId = new PeerId("Abc.Peer.0");
            await _storage.Write(new List<MatcherEntry> { MatcherEntry.Message(peerId, messageId, MessageTypeId.PersistenceStopping, messageBytes) });

            _storage.RemovePeer(peerId);

            _storage.CreateMessageReader(peerId).ShouldBeNull();
            _storage.GetNonAckedMessageCountsForUpdatedPeers().ContainsKey(peerId).ShouldBeFalse();
        }

        [Test]
        public void should_update_non_ack_message_count()
        {
            var firstPeer = new PeerId("Abc.Testing.Target");
            var secondPeer = new PeerId("Abc.Testing.OtherTarget");

            _storage.Write(new[] { MatcherEntry.Message(firstPeer, MessageId.NextId(), new MessageTypeId("Abc.Message"), new byte[] { 0x01, 0x02, 0x03 }) });
            _storage.Write(new[] { MatcherEntry.Message(secondPeer, MessageId.NextId(), new MessageTypeId("Abc.Message"), new byte[] { 0x04, 0x05, 0x06 }) });
            _storage.Write(new[] { MatcherEntry.Message(firstPeer, MessageId.NextId(), new MessageTypeId("Abc.Message"), new byte[] { 0x07, 0x08, 0x09 }) });

            var nonAckedMessageCounts = _storage.GetNonAckedMessageCountsForUpdatedPeers();
            nonAckedMessageCounts[firstPeer].ShouldEqual(2);
            nonAckedMessageCounts[secondPeer].ShouldEqual(1);

            _storage.Write(new[] { MatcherEntry.Ack(firstPeer, MessageId.NextId()) });

            nonAckedMessageCounts = _storage.GetNonAckedMessageCountsForUpdatedPeers();
            nonAckedMessageCounts[firstPeer].ShouldEqual(1);
            nonAckedMessageCounts.ContainsKey(secondPeer).ShouldBeFalse();
        }

        [Test]
        public async Task should_persist_messages_in_order()
        {
            var firstPeer = new PeerId("Abc.Testing.Target");
            var secondPeer = new PeerId("Abc.Testing.OtherTarget");

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

                using (var readerForFirstPeer = (RocksDbMessageReader)_storage.CreateMessageReader(firstPeer))
                {
                    readerForFirstPeer.GetUnackedMessages().ToList().ShouldEqualDeeply(expectedTransportMessages);
                }

                using (var readerForSecondPeer = (RocksDbMessageReader)_storage.CreateMessageReader(secondPeer))
                {
                    readerForSecondPeer.GetUnackedMessages().ToList().ShouldEqualDeeply(expectedTransportMessages);
                }
            }
        }

        [Test, Explicit]
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
