using System;
using System.Linq;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Scan;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using NUnit.Framework;
using StructureMap;

namespace Abc.Zebus.Tests.Scan
{
    [TestFixture]
    public class SyncMessageHandlerInvokerLoaderTests
    {
        [Test]
        public void should_load_queue_name()
        {
            var invokerLoader = new SyncMessageHandlerInvokerLoader(new Container());
            var invokers = invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<FakeHandlerWithQueueName1>()).ToList();

            invokers[0].DispatchQueueName.ShouldEqual("DispatchQueue1");
        }

        [Test]
        public void should_throw_exception_if_method_is_async_void()
        {
            var invokerLoader = new SyncMessageHandlerInvokerLoader(new Container());
            Assert.Throws<InvalidProgramException>(() => invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<WrongAsyncHandler>()).ToList());
        }

        [Test]
        public void should_not_throw_if_scanning_handler_with_several_handle_methods()
        {
            var invokerLoader = new SyncMessageHandlerInvokerLoader(new Container());
            Assert.DoesNotThrow(() => invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<FakeHandler>()).ToList());
        }

        public class FakeMessage : IMessage
        {
        }

        public class FakeMessage2 : IMessage
        {
        }

        public class FakeHandler : IMessageHandler<FakeMessage>, IMessageHandler<FakeMessage2>
        {
            public void Handle(FakeMessage message)
            {
                throw new NotImplementedException();
            }

            public void Handle(FakeMessage2 message)
            {
                throw new NotImplementedException();
            }
        }

        public class WrongAsyncHandler : IMessageHandler<FakeMessage>
        {
            public async void Handle(FakeMessage message)
            {
                await TaskUtil.Completed;
            }
        }

        [DispatchQueueName("DispatchQueue1")]
        public class FakeHandlerWithQueueName1 : IMessageHandler<FakeMessage>
        {
            public void Handle(FakeMessage message)
            {
            }
        }
    }
}