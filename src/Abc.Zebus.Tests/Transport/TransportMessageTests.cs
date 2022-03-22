using System.IO;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Transport;
using NUnit.Framework;
using ProtoBuf;

namespace Abc.Zebus.Tests.Transport
{
    [TestFixture]
    public class TransportMessageTests
    {
        [Test]
        public void should_serialize_transport_message()
        {
            // Arrange
            var transportMessage = TestDataBuilder.CreateTransportMessage<FakeEvent>();

            // Act
            var bytes = TransportMessage.Serialize(transportMessage);

            // Assert
            var stream = new MemoryStream();
            Serializer.Serialize(stream, transportMessage);
            var expectedBytes = stream.ToArray();

            bytes.ShouldEqual(expectedBytes);
        }

        [Test]
        public void should_deserialize_transport_message()
        {
            // Arrange
            var expectedTransportMessage = TestDataBuilder.CreateTransportMessage<FakeEvent>();

            var stream = new MemoryStream();
            Serializer.Serialize(stream, expectedTransportMessage);
            var bytes = stream.ToArray();

            // Act
            var transportMessage = TransportMessage.Deserialize(bytes);

            // Assert
            transportMessage.ShouldEqualDeeply(expectedTransportMessage);
        }
    }
}
