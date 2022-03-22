using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Persistence.Reporter;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Transport;
using Moq;
using NUnit.Framework;
using ProtoBuf;

namespace Abc.Zebus.Persistence.RocksDb.Tests
{
    [TestFixture]
    public class RocksDbStorageTests
    {
        private RocksDbStorage _storage;
        private Mock<IReporter> _reporterMock;
        private string _databaseDirectoryPath;

        [SetUp]
        public void SetUp()
        {
            _databaseDirectoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            _reporterMock = new Mock<IReporter>();
            _storage = new RocksDbStorage(_databaseDirectoryPath);
            _storage.Start();
        }

        [TearDown]
        public void Teardown()
        {
            _storage.Stop();
            System.IO.Directory.Delete(_databaseDirectoryPath, true);
        }

        [Test]
        public async Task should_write_message_entry_fields_to_cassandra()
        {
            var inputMessage = CreateTestTransportMessage(1);
            var messageBytes = TransportMessage.Serialize(inputMessage);
            var messageId = MessageId.NextId();

            var peerId = new PeerId("Abc.Peer.0");
            await _storage.Write(new List<MatcherEntry> { MatcherEntry.Message(peerId, messageId, MessageTypeId.PersistenceStopping, messageBytes) });

            var messages = _storage.CreateMessageReader(peerId).GetUnackedMessages();
            var retrievedMessage = messages.Single();
            retrievedMessage.ShouldEqualDeeply(messageBytes);
        }

        [Test]
        public async Task should_not_overwrite_messages_with_same_time_component_and_different_message_id()
        {
            var messageBytes = TransportMessage.Serialize(CreateTestTransportMessage(1));
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
            var messageBytes = TransportMessage.Serialize(inputMessage);
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
            var messageBytes = TransportMessage.Serialize(inputMessage);
            var messageId = MessageId.NextId();

            var peerId = new PeerId("Abc.Peer.0");
            await _storage.Write(new List<MatcherEntry> { MatcherEntry.Message(peerId, messageId, MessageTypeId.PersistenceStopping, messageBytes) });

            await _storage.RemovePeer(peerId);

            _storage.CreateMessageReader(peerId).ShouldBeNull();
            _storage.GetNonAckedMessageCounts().ContainsKey(peerId).ShouldBeFalse();
        }

        [Test]
        public void should_update_non_ack_message_count()
        {
            var firstPeer = new PeerId("Abc.Testing.Target");
            var secondPeer = new PeerId("Abc.Testing.OtherTarget");

            _storage.Write(new[] { MatcherEntry.Message(firstPeer, MessageId.NextId(), new MessageTypeId("Abc.Message"), new byte[] { 0x01, 0x02, 0x03 }) });
            _storage.Write(new[] { MatcherEntry.Message(secondPeer, MessageId.NextId(), new MessageTypeId("Abc.Message"), new byte[] { 0x04, 0x05, 0x06 }) });
            _storage.Write(new[] { MatcherEntry.Message(firstPeer, MessageId.NextId(), new MessageTypeId("Abc.Message"), new byte[] { 0x07, 0x08, 0x09 }) });

            var nonAckedMessageCounts = _storage.GetNonAckedMessageCounts();
            nonAckedMessageCounts[firstPeer].ShouldEqual(2);
            nonAckedMessageCounts[secondPeer].ShouldEqual(1);

            _storage.Write(new[] { MatcherEntry.Ack(firstPeer, MessageId.NextId()) });

            nonAckedMessageCounts = _storage.GetNonAckedMessageCounts();
            nonAckedMessageCounts[firstPeer].ShouldEqual(1);
            nonAckedMessageCounts[secondPeer].ShouldEqual(1);
        }

        [Test]
        public async Task should_persist_messages_in_order()
        {
            var firstPeer = new PeerId("Abc.Testing.Target");
            var secondPeer = new PeerId("Abc.Testing.OtherTarget");

            using (MessageId.PauseIdGeneration())
            {
                var inputMessages = Enumerable.Range(1, 100).Select(CreateTestTransportMessage).ToList();
                var messages = inputMessages.SelectMany(x =>
                                                        {
                                                            var transportMessageBytes = TransportMessage.Serialize(x);
                                                            return new[]
                                                            {
                                                                MatcherEntry.Message(firstPeer, x.Id, x.MessageTypeId, transportMessageBytes),
                                                                MatcherEntry.Message(secondPeer, x.Id, x.MessageTypeId, transportMessageBytes),
                                                            };
                                                        })
                                                        .ToList();

                await _storage.Write(messages);

                var expectedTransportMessages = inputMessages.Select(TransportMessage.Serialize).ToList();
                using (var readerForFirstPeer = _storage.CreateMessageReader(firstPeer))
                {
                    var transportMessages = readerForFirstPeer.GetUnackedMessages().ToList();
                    transportMessages.Count.ShouldEqual(100);
                    transportMessages.Last().ShouldEqualDeeply(expectedTransportMessages.Last());
                }

                using (var readerForSecondPeer = _storage.CreateMessageReader(secondPeer))
                {
                    var transportMessages = readerForSecondPeer.GetUnackedMessages().ToList();
                    transportMessages.Count.ShouldEqual(100);
                    transportMessages.Last().ShouldEqualDeeply(expectedTransportMessages.Last());
                }
            }
        }

        [Test]
        public async Task should_not_get_acked_message()
        {
            var peer = new PeerId("Abc.Testing.Target");

            var message1 = GetMatcherEntryWithValidTransportMessage(peer, 1);
            var message2 = GetMatcherEntryWithValidTransportMessage(peer, 2);

            await _storage.Write(new[] { message1 });
            await _storage.Write(new[] { message2 });
            await _storage.Write(new[] { MatcherEntry.Ack(peer, message2.MessageId) });

            using (var reader = _storage.CreateMessageReader(peer))
            {
                reader.GetUnackedMessages()
                      .Select(TransportMessage.Deserialize)
                      .Select(x => x.Id)
                      .ToList()
                      .ShouldBeEquivalentTo(message1.MessageId);
            }
        }

        private MatcherEntry GetMatcherEntryWithValidTransportMessage(PeerId peer, int i)
        {
            var inputMessage = CreateTestTransportMessage(i);
            var messageBytes = TransportMessage.Serialize(inputMessage);
            var message1 = MatcherEntry.Message(peer, inputMessage.Id, MessageUtil.TypeId<Message1>(), messageBytes);
            return message1;
        }

        [Test]
        public async Task should_load_previous_out_of_order_acks()
        {
            var peer = new PeerId("Abc.Testing.Target");

            var messageId = MessageId.NextId();
            await _storage.Write(new[] { MatcherEntry.Ack(peer, messageId) });
            _storage.Stop();

            _storage = new RocksDbStorage(_databaseDirectoryPath);
            _storage.Start();

            var message = MatcherEntry.Message(peer, messageId, MessageUtil.TypeId<Message1>(), Array.Empty<byte>());
            await _storage.Write(new[] { message });

            using (var messageReader = _storage.CreateMessageReader(peer))
            {
                messageReader.GetUnackedMessages()
                             .Count()
                             .ShouldEqual(0);
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

        private TransportMessage CreateTestTransportMessage(int i)
        {
            MessageId.PauseIdGenerationAtDate(DateTime.UtcNow.Date.AddSeconds(i * 10));
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
