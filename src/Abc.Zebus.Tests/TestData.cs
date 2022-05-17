using System;
using System.IO;
using Abc.Zebus.Routing;
using Abc.Zebus.Transport;

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
            var contentBytes = new byte[1234];
            new Random().NextBytes(contentBytes);

            var content = new MemoryStream();
            content.Write(contentBytes, 0, contentBytes.Length);

            return new TransportMessage(new MessageTypeId(typeof(TMessage)), content, new PeerId("Abc.Testing.0"), "tcp://testing:1234")
            {
                Environment = "Test",
                WasPersisted = true,
            };
        }
    }
}
