using System;
using System.IO;
using Abc.Zebus.Routing;
using Abc.Zebus.Serialization;
using Abc.Zebus.Transport;
using AutoFixture;

namespace Abc.Zebus.Tests
{
    public static class TestData
    {
        public static Peer Peer()
        {
            var id = Guid.NewGuid();

            return new Peer(new PeerId($"Abc.Testing.{id}"), $"tcp://testingendpoint:{(ushort)id.GetHashCode()}");
        }

        public static TransportMessage TransportMessage<TMessage>()
        {
            var fixture = new Fixture();
            var message = fixture.Create<TMessage>();
            var content = ProtoBufConvert.Serialize(message);

            return new TransportMessage(new MessageTypeId(typeof(TMessage)), content, new PeerId("Abc.Testing.0"), "tcp://testing:1234")
            {
                Environment = "Test",
                WasPersisted = true,
            };
        }
    }
}
