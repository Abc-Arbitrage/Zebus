using System.Collections.Generic;
using System.Threading.Tasks;
using Abc.Zebus.Core;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
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
            bus.Stop();

            message.HandledSync.ShouldBeTrue();
            message.HandledAsync.ShouldBeTrue();
            message.HandledBatched.ShouldBeTrue();
        }

        public class TestMessage : IEvent
        {
            public bool HandledSync { get; set; }
            public bool HandledAsync { get; set; }
            public bool HandledBatched { get; set; }
        }

        public class SyncMessageHandler : IMessageHandler<TestMessage>
        {
            public void Handle(TestMessage message)
            {
                message.HandledSync = true;
            }
        }

        public class AsyncMessageHandler : IAsyncMessageHandler<TestMessage>
        {
            public Task Handle(TestMessage message)
            {
                message.HandledAsync = true;
                return TaskUtil.Completed;
            }
        }

        public class BatchedMessageHandler : IBatchedMessageHandler<TestMessage>
        {
            public void Handle(IList<TestMessage> messages)
            {
                foreach (var message in messages)
                    message.HandledBatched = true;
            }
        }
    }
}
