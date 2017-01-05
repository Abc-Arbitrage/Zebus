using System;
using System.IO;
using System.Reflection;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Transport;
using NUnit.Framework;
using ProtoBuf;

namespace Abc.Zebus.Tests.Transport
{
    public class BackwardCompatibilityTests
    {
        [Test]
        public void should_deserialize_1_4_1_transport_messages()
        {
            var content = new MemoryStream(new byte[] { 1, 2, 3 });
            var originatorInfo = new OriginatorInfo(new PeerId("peer"), "endpoint", "MACHINEXXX", "username");
            var messageId = new MessageId(Guid.Parse("ce0ac850-a9c5-e511-932e-d8e94a2d2418"));
            var expectedMessage = new TransportMessage(new MessageTypeId("lol"), content, originatorInfo, messageId);

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Abc.Zebus.Tests.Transport.transport_message_1_4_1.bin"))
            {
                var message = Serializer.Deserialize<TransportMessage>(stream);

                message.ShouldHaveSamePropertiesAs(expectedMessage);
            }
        }

        [Test]
        public void should_read_WasPersisted_as_null_for_older_versions()
        {
            var content = new MemoryStream(new byte[] { 1, 2, 3 });
            var originatorInfo = new OriginatorInfo(new PeerId("peer"), "endpoint", "MACHINEXXX", "username");
            var messageId = new MessageId(Guid.Parse("ce0ac850-a9c5-e511-932e-d8e94a2d2418"));
            var expectedMessage = new TransportMessage(new MessageTypeId("lol"), content, originatorInfo, messageId) { WasPersisted = false };

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Abc.Zebus.Tests.Transport.transport_message_1_4_1.bin"))
            {
                var message = Serializer.Deserialize<TransportMessage>(stream);

                message.ShouldHaveSamePropertiesAs(expectedMessage, "WasPersisted");
                message.WasPersisted.ShouldBeNull();
            }
        }
    }
}