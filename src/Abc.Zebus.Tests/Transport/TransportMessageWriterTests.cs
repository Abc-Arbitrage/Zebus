using System.Collections.Generic;
using System.IO;
using System.Text;
using Abc.Zebus.Serialization.Protobuf;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Testing.Measurements;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Tests.Transport.V1_5_0;
using Abc.Zebus.Transport;
using NUnit.Framework;
using ProtoBuf;

namespace Abc.Zebus.Tests.Transport
{
    [TestFixture]
    public class TransportMessageWriterTests
    {
        [Test]
        public void should_serialize_transport_message_and_read_from_protobuf()
        {
            var transportMessage = TestDataBuilder.CreateTransportMessage<FakeCommand>();

            var stream = new ProtoBufferWriter();
            stream.WriteTransportMessage(transportMessage);

            var deserialized = Serializer.Deserialize<TransportMessage>(new MemoryStream(stream.Buffer, 0, stream.Position));
            deserialized.Id.ShouldEqual(transportMessage.Id);
            deserialized.MessageTypeId.ShouldEqual(transportMessage.MessageTypeId);
            deserialized.GetContentBytes().ShouldEqual(transportMessage.GetContentBytes());
            deserialized.Originator.ShouldEqualDeeply(transportMessage.Originator);
            deserialized.Originator.SenderMachineName.ShouldEqual(transportMessage.Originator.SenderMachineName);
            deserialized.Environment.ShouldEqual(transportMessage.Environment);
            deserialized.WasPersisted.ShouldEqual(true);
        }

        [Test]
        public void should_serialize_transport_message_and_read_from_protobuf_1_5_0()
        {
            var transportMessage = TestDataBuilder.CreateTransportMessage<FakeCommand>();

            var stream = new ProtoBufferWriter();
            stream.WriteTransportMessage(transportMessage);

            var deserialized = Serializer.Deserialize<TransportMessage_1_5_0>(new MemoryStream(stream.Buffer, 0, stream.Position));
            deserialized.Id.ShouldEqual(transportMessage.Id);
            deserialized.MessageTypeId.ShouldEqual(transportMessage.MessageTypeId);
            deserialized.Content.ShouldEqual(transportMessage.GetContentBytes());
            deserialized.Originator.SenderId.ShouldEqual(transportMessage.Originator.SenderId);
            deserialized.Originator.SenderEndPoint.ShouldEqual(transportMessage.Originator.SenderEndPoint);
            deserialized.Originator.SenderMachineName.ShouldEqual(transportMessage.Originator.SenderMachineName);
            deserialized.Originator.InitiatorUserName.ShouldEqual(transportMessage.Originator.InitiatorUserName);
            deserialized.Environment.ShouldEqual(transportMessage.Environment);
            deserialized.WasPersisted.ShouldEqual(true);
        }

        [Test]
        public void should_serialize_transport_message_with_persistent_peer_ids_and_read_from_protobuf()
        {
            var transportMessage = TestDataBuilder.CreateTransportMessage<FakeCommand>();
            transportMessage.PersistentPeerIds = new List<PeerId>
            {
                new PeerId("Abc.Testing.A"),
                new PeerId("Abc.Testing.B"),
            };

            var stream = new ProtoBufferWriter();
            stream.WriteTransportMessage(transportMessage);
            stream.WritePersistentPeerIds(transportMessage, transportMessage.PersistentPeerIds);

            var deserialized = Serializer.Deserialize<TransportMessage>(new MemoryStream(stream.Buffer, 0, stream.Position));
            deserialized.Id.ShouldEqual(transportMessage.Id);
            deserialized.MessageTypeId.ShouldEqual(transportMessage.MessageTypeId);
            deserialized.PersistentPeerIds.ShouldBeEquivalentTo(transportMessage.PersistentPeerIds);
        }

        [Test]
        public void should_serialize_transport_message_with_empty_strings()
        {
            var transportMessage = TestDataBuilder.CreateTransportMessage<FakeCommand>();
            transportMessage.Environment = null;
            transportMessage.Originator = new OriginatorInfo(new PeerId(null), null, null, null);

            var stream = new ProtoBufferWriter();
            stream.WriteTransportMessage(transportMessage);

            var deserializedTransportMessage1 = Serializer.Deserialize<TransportMessage>(new MemoryStream(stream.Buffer, 0, stream.Position));
            deserializedTransportMessage1.Id.ShouldEqual(transportMessage.Id);
            deserializedTransportMessage1.MessageTypeId.ShouldEqual(transportMessage.MessageTypeId);
            deserializedTransportMessage1.GetContentBytes().ShouldEqual(transportMessage.GetContentBytes());
            deserializedTransportMessage1.Originator.InitiatorUserName.ShouldEqual(transportMessage.Originator.InitiatorUserName);
            deserializedTransportMessage1.Environment.ShouldEqual(transportMessage.Environment);
            deserializedTransportMessage1.WasPersisted.ShouldEqual(true);
        }

        [TestCase(null, true)]
        [TestCase(null, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void should_serialize_transport_message_and_set_WasPersisted(bool? previousWasPersistedValue, bool newWasPersistedValue)
        {
            var transportMessage = TestDataBuilder.CreateTransportMessage<FakeCommand>();
            transportMessage.WasPersisted = previousWasPersistedValue;

            var stream = new ProtoBufferWriter();
            stream.WriteTransportMessage(transportMessage);
            stream.SetWasPersisted(newWasPersistedValue);

            var deserializedTransportMessage1 = Serializer.Deserialize<TransportMessage>(new MemoryStream(stream.Buffer, 0, stream.Position));
            deserializedTransportMessage1.Id.ShouldEqual(transportMessage.Id);
            deserializedTransportMessage1.Environment.ShouldEqual(transportMessage.Environment);
            deserializedTransportMessage1.WasPersisted.ShouldEqual(newWasPersistedValue);
        }

        [Test]
        public void should_serialize_transport_message_twice()
        {
            var transportMessage = TestDataBuilder.CreateTransportMessage<FakeCommand>();

            var stream = new ProtoBufferWriter();
            stream.WriteTransportMessage(transportMessage);

            var deserialized1 = Serializer.Deserialize<TransportMessage_1_5_0>(new MemoryStream(stream.Buffer, 0, stream.Position));
            deserialized1.WasPersisted.ShouldEqual(true);

            stream.Reset();
            transportMessage.WasPersisted = false;
            stream.WriteTransportMessage(transportMessage);

            var deserialized2 = Serializer.Deserialize<TransportMessage_1_5_0>(new MemoryStream(stream.Buffer, 0, stream.Position));
            deserialized2.WasPersisted.ShouldEqual(false);
        }

        [Test, Explicit("Manual test")]
        public void MeasureWritePerformance()
        {
            var transportMessage = TestDataBuilder.CreateTransportMessage<FakeCommand>();
            var stream = new ProtoBufferWriter();

            stream.WriteTransportMessage(transportMessage);

            const int count = 10 * 1000 * 1000;
            using (Measure.Throughput(count))
            {
                for (var i = 0; i < count; i++)
                {
                    stream.Reset();
                    stream.WriteTransportMessage(transportMessage);
                }
            }
        }
    }
}