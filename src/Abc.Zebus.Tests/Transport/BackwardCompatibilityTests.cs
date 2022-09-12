using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Abc.Zebus.Serialization.Protobuf;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Transport.V1_5_0;
using Abc.Zebus.Transport;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Transport
{
    public class BackwardCompatibilityTests
    {
        [Test]
        public void should_deserialize_1_4_1_transport_messages()
        {
            var content = new byte[] { 1, 2, 3 };
            var originatorInfo = new OriginatorInfo(new PeerId("peer"), "endpoint", "MACHINEXXX", "username");
            var messageId = new MessageId(Guid.Parse("ce0ac850-a9c5-e511-932e-d8e94a2d2418"));
            var expectedMessage = new TransportMessage(new MessageTypeId("lol"), content, originatorInfo) { Id = messageId };

            var stream = GetTransportMessageStream_1_4_1();
            var bufferReader = new ProtoBufferReader(stream.GetBuffer(), (int)stream.Length);

            var message = bufferReader.ReadTransportMessage();
            message.ShouldHaveSamePropertiesAs(expectedMessage);
        }

        [Test]
        public void should_read_WasPersisted_as_null_for_older_versions()
        {
            var content = new byte[] { 1, 2, 3 };
            var originatorInfo = new OriginatorInfo(new PeerId("peer"), "endpoint", "MACHINEXXX", "username");
            var messageId = new MessageId(Guid.Parse("ce0ac850-a9c5-e511-932e-d8e94a2d2418"));
            var expectedMessage = new TransportMessage(new MessageTypeId("lol"), content, originatorInfo) { Id = messageId, WasPersisted = false };

            var stream = GetTransportMessageStream_1_4_1();
            var bufferReader = new ProtoBufferReader(stream.GetBuffer(), (int)stream.Length);

            var message = bufferReader.ReadTransportMessage();
            message.ShouldHaveSamePropertiesAs(expectedMessage, "WasPersisted");
            message.WasPersisted.ShouldBeNull();
        }

        [Test]
        public void should_serialize_empty_messages_like_1_5_0()
        {
            var oldTransportMessage = new TransportMessage_1_5_0
            {
                Id = new MessageId(Guid.NewGuid()),
                Originator = new OriginatorInfo_1_5_0(),
                Content = new byte[0],
            };
            var oldOutput = new MemoryStream();
            ProtoBuf.Serializer.Serialize(oldOutput, oldTransportMessage);

            var newTransportMessage = TransportMessage.Empty();
            newTransportMessage.Id = oldTransportMessage.Id;

            var bufferWriter = new ProtoBufferWriter();
            bufferWriter.WriteTransportMessage(newTransportMessage);

            bufferWriter.ToArray().SequenceEqual(oldOutput.ToArray()).ShouldBeTrue();
        }

        private MemoryStream GetTransportMessageStream_1_4_1()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Abc.Zebus.Tests.Transport.transport_message_1_4_1.bin"))
            {
                var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);

                return memoryStream;
            }
        }
    }
}
