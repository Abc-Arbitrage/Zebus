using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.LMDB.Storage;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Testing;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using NUnit.Framework;
using ProtoBuf;

namespace Abc.Zebus.Persistence.LMDB.Tests
{
    [TestFixture, Explicit]
    public class PerformanceTests
    {
        private LmdbStorage _storage;

        [SetUp]
        public void SetUp()
        {
            _storage = new LmdbStorage(Guid.NewGuid().ToString());
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
            var testDuration = 30.Seconds();//1.Minute();
            // Thread 1 - write messages, then ack them 2 seconds later
            var writeTask = Task.Run(async () =>
            {
                var count = 0;
                while (DateTime.UtcNow - startTime < testDuration)
                {
                    var entriesToPersist = GetEntriesToPersist(messageBytes);
                    await _storage.Write(entriesToPersist);

                    // Thread.Sleep(2.Seconds());

                    await _storage.Write(ToAckEntries(entriesToPersist));

                    count++;
                }

                Console.WriteLine($"Wrote {count * 100} messages");
            });

            // Thread 2 - ask for all unacked messages every 1 seconds
            var updatesTask = Task.Run(() =>
            {
                var count = 0;
                while (DateTime.UtcNow - startTime < testDuration)
                {
                    _storage.GetNonAckedMessageCounts();
                    count++;
                    // Thread.Sleep(1.Second());
                }

                Console.WriteLine($"Got non acked message counts {count} times");
            });

            await Task.WhenAll(writeTask, updatesTask);
        }

        private List<MatcherEntry> ToAckEntries(List<MatcherEntry> entriesToPersist)
        {
            return entriesToPersist.Select(x => MatcherEntry.Ack(x.PeerId, x.MessageId)).ToList();
        }

        private static List<MatcherEntry> GetEntriesToPersist(byte[] messageBytes)
        {
            var entriesToPersist = new List<MatcherEntry>();
            for (int i = 0; i < 100; i++)
            {
                entriesToPersist.Add(MatcherEntry.Message(new PeerId("Peer1"), MessageId.NextId(), new MessageTypeId("SomeEvent"), messageBytes));
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
