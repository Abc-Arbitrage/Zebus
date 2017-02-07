using System;
using System.IO;
using System.Linq;
using Abc.Zebus.Persistence.CQL.Data;
using Abc.Zebus.Persistence.CQL.Storage;
using Abc.Zebus.Persistence.CQL.Tests.Cql;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Serialization;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Transport;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.CQL.Tests
{
    public class CqlMessageReaderTests : CqlTestFixture<PersistenceCqlDataContext, ICqlPersistenceConfiguration>
    {
        private readonly Serializer _serializer = new Serializer();

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
            nonAckedMessages.ShouldBeEquivalentTo(transportMessages, true);
        }

        private Action<PersistentMessage> UpdatePersistentMessageWithNonAckedTransportMessage(TransportMessage transportMessage)
        {
            return x =>
            {
                x.IsAcked = false;
                x.TransportMessage = _serializer.Serialize(transportMessage).ToArray();
            };
        }

        private TransportMessage CreateTransportMessage(PeerId peerId)
        {
            var bytes = new byte[128];
            new Random().NextBytes(bytes);
            return new TransportMessage(new MessageTypeId("Fake"), new MemoryStream(bytes), new Peer(peerId, string.Empty));
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