using System.Collections.Generic;
using System.Threading.Tasks;
using Abc.Zebus.Core;
using Abc.Zebus.Testing;
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Scan
{
    [TestFixture]
    public class MessageHandlerInvokerLoaderTests
    {
        [Test]
        public void should_create_all_invoker_types()
        {
            var bus = new BusFactory()
                .WithHandlers(
                    typeof(SyncMessageHandler),
                    typeof(AsyncMessageHandler),
                    typeof(BatchedMessageHandler)
                )
                .CreateAndStartInMemoryBus();

            var message = new TestMessage();
            bus.Publish(message);

            Wait.Until(() => message.HandledSync, 2.Seconds());
            Wait.Until(() => message.HandledAsync, 2.Seconds());
            Wait.Until(() => message.HandledBatched, 2.Seconds());
        }

        [Test]
        public void should_support_explicit_interface_implementations()
        {
            var bus = new BusFactory()
                .WithHandlers(
                    typeof(SyncMessageHandler),
                    typeof(AsyncMessageHandler),
                    typeof(BatchedMessageHandler)
                )
                .CreateAndStartInMemoryBus();

            var message = new TestExplicitImplMessage();
            bus.Publish(message);

            Wait.Until(() => message.HandledSync, 2.Seconds());
            Wait.Until(() => message.HandledAsync, 2.Seconds());
            Wait.Until(() => message.HandledBatched, 2.Seconds());
        }

        public class TestMessage : IEvent
        {
            public bool HandledSync { get; set; }
            public bool HandledAsync { get; set; }
            public bool HandledBatched { get; set; }
        }

        public class TestExplicitImplMessage : IEvent
        {
            public bool HandledSync { get; set; }
            public bool HandledAsync { get; set; }
            public bool HandledBatched { get; set; }
        }

        public class SyncMessageHandler : IMessageHandler<TestMessage>,
                                          IMessageHandler<TestExplicitImplMessage>
        {
            public void Handle(TestMessage message)
            {
                message.HandledSync = true;
            }

            void IMessageHandler<TestExplicitImplMessage>.Handle(TestExplicitImplMessage message)
            {
                message.HandledSync = true;
            }
        }

        public class AsyncMessageHandler : IAsyncMessageHandler<TestMessage>,
                                           IAsyncMessageHandler<TestExplicitImplMessage>
        {
            public Task Handle(TestMessage message)
            {
                message.HandledAsync = true;
                return Task.CompletedTask;
            }

            Task IAsyncMessageHandler<TestExplicitImplMessage>.Handle(TestExplicitImplMessage message)
            {
                message.HandledAsync = true;
                return Task.CompletedTask;
            }
        }

        public class BatchedMessageHandler : IBatchedMessageHandler<TestMessage>,
                                             IBatchedMessageHandler<TestExplicitImplMessage>
        {
            public void Handle(IList<TestMessage> messages)
            {
                foreach (var message in messages)
                    message.HandledBatched = true;
            }

            void IBatchedMessageHandler<TestExplicitImplMessage>.Handle(IList<TestExplicitImplMessage> messages)
            {
                foreach (var message in messages)
                    message.HandledBatched = true;
            }
        }
    }
}
