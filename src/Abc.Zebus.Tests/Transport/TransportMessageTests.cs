using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Serialization;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using NUnit.Framework;
using ProtoBuf;
using Serializer = ProtoBuf.Serializer;

namespace Abc.Zebus.Tests.Transport
{
    [TestFixture]
    public class TransportMessageTests
    {
        [Test]
        public void should_serialize_transport_message()
        {
            // Arrange
            var transportMessage = TestData.TransportMessage<FakeEvent>();

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
            var expectedTransportMessage = TestData.TransportMessage<FakeEvent>();

            var stream = new MemoryStream();
            Serializer.Serialize(stream, expectedTransportMessage);
            var bytes = stream.ToArray();

            // Act
            var transportMessage = TransportMessage.Deserialize(bytes);

            // Assert
            transportMessage.ShouldEqualDeeply(expectedTransportMessage);
        }

        [Test, Repeat(10)]
        public void should_serialize_from_multiple_threads()
        {
            // Arrange
            const int threadCount = 100;

            var serializer = new MessageSerializer();
            var message = new TestMessage(Guid.NewGuid());
            var transportMessage = serializer.ToTransportMessage(message, new PeerId("Abc.X.0"), "tcp://abctest:123");
            var serializedTransportMessages = new List<byte[]>();
            var signal = new ManualResetEventSlim();

            // Act
            for (var i = 0; i < threadCount; i++)
            {
                Task.Run(() =>
                {
                    signal.Wait(10.Seconds()).ShouldBeTrue();

                    var bytes = TransportMessage.Serialize(transportMessage);
                    lock (serializedTransportMessages)
                    {
                        serializedTransportMessages.Add(bytes);
                    }
                });
            }

            signal.Set();

            // Assert
            Wait.Until(() => serializedTransportMessages.Count == threadCount, 5.Seconds());

            foreach (var serializedTransportMessage in serializedTransportMessages)
            {
                var deserializedTransportMessage = TransportMessage.Deserialize(serializedTransportMessage);
                deserializedTransportMessage.ShouldEqualDeeply(transportMessage);

                var deserializedMessage = serializer.ToMessage(deserializedTransportMessage);
                deserializedMessage.ShouldEqualDeeply(message);
            }
        }

        [ProtoContract]
        private class TestMessage : IEvent
        {
            public TestMessage(Guid id)
            {
                Id = id;
            }

            [ProtoMember(1)]
            public Guid Id { get; set; }
        }
    }
}
