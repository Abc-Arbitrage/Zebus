using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Testing.Dispatch;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Dispatch.DispatchMessages;
using Moq;
using NUnit.Framework;
using StructureMap;

namespace Abc.Zebus.Tests.Dispatch
{
    [TestFixture]
    public class BatchedMessageHandlerInvokerTests
    {
        [Test]
        public void should_invoke_handler()
        {
            var handler = new Handler();
            var container = new Container(x => x.ForSingletonOf<Handler>().Use(handler));
            var messages = new List<IMessage>
            {
                new Message { Id = 1 },
                new Message { Id = 2 },
            };

            var invocationMock = new Mock<IMessageHandlerInvocation>();
            invocationMock.SetupGet(x => x.Context).Returns(MessageContext.CreateTest());
            invocationMock.SetupGet(x => x.Messages).Returns(messages);

            var invoker = new BatchedMessageHandlerInvoker(container, typeof(Handler), typeof(Message));
            invoker.InvokeMessageHandler(invocationMock.Object);

            handler.Messages.ShouldEqual(messages.Cast<Message>().ToList());
        }

        private class Message : IEvent
        {
            public int Id;

            public override string ToString() => Id.ToString();
        }

        private class Handler : IBatchMessageHandler<Message>
        {
            public readonly List<Message> Messages = new List<Message>();

            public void Handle(List<Message> messages)
            {
                Messages.AddRange(messages);
            }
        }
    }
}