using System;
using System.IO;
using System.Linq;
using Abc.Zebus.Persistence.Cassandra.Cql;
using Abc.Zebus.Persistence.Cassandra.Data;
using Abc.Zebus.Persistence.Cassandra.Tests.Cql;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Persistence.Storage;
using Abc.Zebus.Serialization;
using Abc.Zebus.Testing.Comparison;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Transport;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.Cassandra.Tests
{
    public class CqlMessageReaderTests : CqlTestFixture<PersistenceCqlDataContext, ICqlPersistenceConfiguration>
    {
        public override void CreateSchema()
        {
            IgnoreWhenSet("GITHUB_ACTIONS");
            base.CreateSchema();
        }

        private static void IgnoreWhenSet(string environmentVariable)
        {
            var env = Environment.GetEnvironmentVariable(environmentVariable);
            if (!string.IsNullOrEmpty(env) && bool.TryParse(env, out var isSet) && isSet)
                Assert.Ignore("We need a cassandra node for this");
        }

        [Test]
        public void should_read_non_acked_messages_since_oldest()
        {
            var peerId = new PeerId("PeerId");
            var now = DateTime.UtcNow;
            var oldestNonAckedMessageTimestamp = now.AddHours(-2).AddMilliseconds(1);
            var transportMessages = new[]
            {
                CreateTransportMessage(peerId),
                CreateTransportMessage(peerId),
                CreateTransportMessage(peerId),
            };
            var reader = CreateReader(peerId, oldestNonAckedMessageTimestamp);

            // first bucket - all acked
            InsertPersistentMessage(peerId, now.AddHours(-2), x => x.IsAcked = true);
            InsertPersistentMessage(peerId, oldestNonAckedMessageTimestamp, UpdatePersistentMessageWithNonAckedTransportMessage(transportMessages[0]));
            InsertPersistentMessage(peerId, now.AddHours(-2).AddMilliseconds(2), x => x.IsAcked = true);
            // second bucket - with non acked
            InsertPersistentMessage(peerId, now.AddHours(-1), x => x.IsAcked = true);
            InsertPersistentMessage(peerId, now.AddHours(-1).AddMilliseconds(1), UpdatePersistentMessageWithNonAckedTransportMessage(transportMessages[1]));
            InsertPersistentMessage(peerId, now.AddHours(-1).AddMilliseconds(2), x => x.IsAcked = true);
            // third bucket - with non acked
            InsertPersistentMessage(peerId, now.AddMilliseconds(-3), x => x.IsAcked = true);
            InsertPersistentMessage(peerId, now.AddMilliseconds(-2), UpdatePersistentMessageWithNonAckedTransportMessage(transportMessages[2]));
            InsertPersistentMessage(peerId, now.AddMilliseconds(-1), x => x.IsAcked = true);

            var nonAckedMessages = reader.GetUnackedMessages().ToList();
            nonAckedMessages.Count.ShouldEqual(3);
            for (var i = 0; i < nonAckedMessages.Count; i++)
            {
                var transportMessage = TransportMessage.Deserialize(nonAckedMessages[i]);
                transportMessage.DeepCompare(transportMessages[i]).ShouldBeTrue();
            }
        }

        private Action<PersistentMessage> UpdatePersistentMessageWithNonAckedTransportMessage(TransportMessage transportMessage)
        {
            return x =>
            {
                x.IsAcked = false;
                x.TransportMessage = TransportMessage.Serialize(transportMessage);
            };
        }

        private TransportMessage CreateTransportMessage(PeerId peerId)
        {
            var bytes = new byte[128];
            new Random().NextBytes(bytes);
            return new TransportMessage(new MessageTypeId("Fake"), bytes, new Peer(peerId, string.Empty));
        }

        private CqlMessageReader CreateReader(PeerId peerId, DateTime oldestNonAckedMessage)
        {
            return new CqlMessageReader(DataContext, new PeerState(peerId, 0, oldestNonAckedMessage.Ticks));
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
