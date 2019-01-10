using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Testing;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using NUnit.Framework;
using ProtoBuf;

namespace Abc.Zebus.Persistence.RocksDb.Tests
{
    [TestFixture, Explicit]
    public class PerformanceTests
    {
        private RocksDbStorage _storage;

        [SetUp]
        public void SetUp()
        {
            _storage = new RocksDbStorage(Guid.NewGuid().ToString());
            _storage.Start();
        }

        [TearDown]
        public void Teardown()
        {
            _storage.Stop();
        }

        [Test]
        public async Task should_do_this()
        {
            var messageBytes = MessageBytes();

            var startTime = DateTime.UtcNow;
            var testDuration = 30.Seconds();
            // Thread 1 - write messages
            var writeTask = Task.Run(async () =>
            {
                var count = 0;
                while (DateTime.UtcNow - startTime < testDuration)
                {
                    var entriesToPersist = GetEntriesToPersist(messageBytes, count);
                    await _storage.Write(entriesToPersist);

                    // Thread.Sleep(2.Seconds());

                    await _storage.Write(ToAckEntries(entriesToPersist));

                    count++;
                }

                Console.WriteLine($"Wrote {count * 100:N0} messages");
            });

            // Thread 2 - ask for all unacked messages
            /*
            var updatesTask = Task.Run(() =>
            {
                var count = 0;
                while (DateTime.UtcNow - startTime < testDuration)
                {
                    _storage.GetNonAckedMessageCountsForUpdatedPeers();
                    count++;
                    // Thread.Sleep(100.Milliseconds());
                }

                Console.WriteLine($"Got non acked message counts {count:N0} times");
            });
            */

            await Task.WhenAll(writeTask/*, updatesTask*/);
        }

        [Test]
        public void should_test_replay()
        {
            var messageBytes = MessageBytes();

            // Fill with lots of unacked messages
            var entriesToPersist = new List<MatcherEntry>();
            var peerId = new PeerId("Peer");
            for (int i = 0; i < 10_000; i++)
            {
                MessageId.PauseIdGenerationAtDate(SystemDateTime.UtcNow.Date.AddSeconds(i * 10));
                entriesToPersist.Add(MatcherEntry.Message(peerId, MessageId.NextId(), new MessageTypeId("SomeEvent"), messageBytes));
            }

            _storage.Write(entriesToPersist);

            // Read all unacked messages 
            var messageReader = _storage.CreateMessageReader(peerId);
            var startTime = DateTime.UtcNow;
            var testDuration = 30.Seconds();
            var count = 0;
            while (DateTime.UtcNow - startTime < testDuration)
            {
                foreach (var transportMessage in messageReader.GetUnackedMessages()) { }

                count++;
            }

            Console.WriteLine($"Replayed {count:N0} times ({count*entriesToPersist.Count:N0} messages) in {testDuration.TotalSeconds:N0}s");
        }

        private List<MatcherEntry> ToAckEntries(List<MatcherEntry> entriesToPersist)
        {
            return entriesToPersist.Select(x => MatcherEntry.Ack(x.PeerId, x.MessageId)).ToList();
        }

        private static List<MatcherEntry> GetEntriesToPersist(byte[] messageBytes, int offset, int count = 100)
        {
            var entriesToPersist = new List<MatcherEntry>();
            for (int i = 0; i < count; i++)
            {
                MessageId.PauseIdGenerationAtDate(SystemDateTime.UtcNow.Date.AddSeconds(i * 10));
                entriesToPersist.Add(MatcherEntry.Message(new PeerId("Peer" + (i + offset)), MessageId.NextId(), new MessageTypeId("SomeEvent"), messageBytes));
            }
            return entriesToPersist;
        }

        private static byte[] MessageBytes()
        {
            var message = CreateTestTransportMessage(1);
            var messageBytes = Serialization.Serializer.Serialize(message).ToArray();
            return messageBytes;
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

            [ProtoMember(2, IsRequired = true)]
            public long Data1 { get; set; }

            [ProtoMember(3, IsRequired = true)]
            public Guid Data2 { get; set; }

            [ProtoMember(4, IsRequired = true)]
            public DateTime Data3 { get; set; }

            public Message1(int id)
            {
                Id = id;
            }
        }
    }
}
