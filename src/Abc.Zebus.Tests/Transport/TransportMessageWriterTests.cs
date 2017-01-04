using System.IO;
using System.Text;
using Abc.Zebus.Serialization.Protobuf;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Transport;
using NUnit.Framework;
using ProtoBuf;

namespace Abc.Zebus.Tests.Transport
{
    [TestFixture]
    public class TransportMessageWriterTests
    {
        [Test]
        public void should_serialize_transport_message()
        {
            var transportMessage = TestDataBuilder.CreateTransportMessage<FakeCommand>();

            var stream = new CodedOutputStream();
            TransportMessageWriter.Write(stream, transportMessage);

            var deserializedTransportMessage1 = Serializer.Deserialize<TransportMessage>(new MemoryStream(stream.Buffer, 0, stream.Position));
            deserializedTransportMessage1.Id.ShouldEqual(transportMessage.Id);
            deserializedTransportMessage1.MessageTypeId.ShouldEqual(transportMessage.MessageTypeId);
            deserializedTransportMessage1.GetContentBytes().ShouldEqual(transportMessage.GetContentBytes());
            deserializedTransportMessage1.Originator.InitiatorUserName.ShouldEqual(transportMessage.Originator.InitiatorUserName);
            deserializedTransportMessage1.Environment.ShouldEqual(transportMessage.Environment);
            deserializedTransportMessage1.WasPersisted.ShouldEqual(true);

            var deserializedTransportMessage2 = Serializer.Deserialize<TransportMessage2>(new MemoryStream(stream.Buffer, 0, stream.Position));
            deserializedTransportMessage2.Id.ShouldEqual(transportMessage.Id);
            deserializedTransportMessage2.MessageTypeId.ShouldEqual(transportMessage.MessageTypeId);
            deserializedTransportMessage2.Content.ShouldEqual(transportMessage.GetContentBytes());
            deserializedTransportMessage2.Originator.InitiatorUserName.ShouldEqual(transportMessage.Originator.InitiatorUserName);
            deserializedTransportMessage2.Environment.ShouldEqual(transportMessage.Environment);
            deserializedTransportMessage2.WasPersisted.ShouldEqual(true);
        }

        [Test]
        public void should_serialize_transport_message_twice()
        {
            var contentBytes = Encoding.ASCII.GetBytes(new string('X', 1000));
            var content = new MemoryStream(contentBytes);
            var transportMessage = new TransportMessage(new MessageTypeId(typeof(FakeCommand)), content, new PeerId("Abc.Testing.0"), "tcp://testing:1234", MessageId.NextId())
            {
                Environment = "Test",
                WasPersisted = true,
            };

            var stream = new CodedOutputStream();
            TransportMessageWriter.Write(stream, transportMessage);

            var deserialized1 = Serializer.Deserialize<TransportMessage2>(new MemoryStream(stream.Buffer, 0, stream.Position));
            deserialized1.WasPersisted.ShouldEqual(true);

            stream.Position = 0;
            transportMessage.WasPersisted = false;
            TransportMessageWriter.Write(stream, transportMessage);

            var deserialized2 = Serializer.Deserialize<TransportMessage2>(new MemoryStream(stream.Buffer, 0, stream.Position));
            deserialized2.WasPersisted.ShouldEqual(false);
        }

        [ProtoContract]
        public class TransportMessage2
        {
            [ProtoMember(1, IsRequired = true)]
            public readonly MessageId Id;

            [ProtoMember(2, IsRequired = true)]
            public readonly MessageTypeId MessageTypeId;

            [ProtoMember(3, IsRequired = true)]
            public readonly byte[] Content;

            [ProtoMember(4, IsRequired = true)]
            public readonly OriginatorInfo Originator;

            [ProtoMember(5, IsRequired = false)]
            public string Environment { get; set; }

            [ProtoMember(6, IsRequired = false)]
            public bool? WasPersisted { get; set; }

        }
    }
}