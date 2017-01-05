using System;
using System.IO;
using Abc.Zebus.Routing;
using Abc.Zebus.Transport;

namespace Abc.Zebus.Tests
{
    public static class TestDataBuilder
    {
         public static Subscription[] CreateSubscriptions<TMessage>() where TMessage : IMessage
         {
             return new[] { CreateSubscription<TMessage>() };
         }

         public static Subscription CreateSubscription<TMessage>(BindingKey? bindingKey = null) where TMessage : IMessage
         {
             return new Subscription(MessageUtil.TypeId<TMessage>(), bindingKey ?? BindingKey.Empty);
         }

        public static TransportMessage CreateTransportMessage<TMessage>()
        {
            var content = new byte[1234];
            new Random().NextBytes(content);

            return CreateTransportMessage<TMessage>(CreateStream(content));
        }

        public static MemoryStream CreateStream(byte[] content)
        {
            var contentStream = new MemoryStream();
            contentStream.Write(content, 0, content.Length);
            return contentStream;
        }

        public static TransportMessage CreateTransportMessage<TMessage>(Stream content)
        {
            return new TransportMessage(new MessageTypeId(typeof(TMessage)), content, new PeerId("Abc.Testing.0"), "tcp://testing:1234", MessageId.NextId())
            {
                Environment = "Test",
                WasPersisted = true,
            };
        }
    }
}