using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.DependencyInjection;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Dispatch.Pipes;
using Abc.Zebus.Scan;
using Abc.Zebus.Testing.Extensions;
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

            var containerProvider = new StructureMapContainerProvider(container);
            var invoker = new BatchedMessageHandlerInvoker(containerProvider, typeof(Handler), typeof(Message));
            var invocation = new PipeInvocation(invoker, messages, MessageContext.CreateTest(), new IPipe[0]);

            invocation.Run();

            handler.Messages.ShouldEqual(messages.Cast<Message>().ToList());
        }

        private class Message : IEvent
        {
            public int Id;

            public override string ToString() => Id.ToString();
        }

        private class Handler : IBatchedMessageHandler<Message>
        {
            public readonly List<Message> Messages = new List<Message>();

            public void Handle(IList<Message> messages)
            {
                Messages.AddRange(messages);
            }
        }
    }
}
