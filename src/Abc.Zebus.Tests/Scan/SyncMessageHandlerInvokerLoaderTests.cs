using System;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Routing;
using Abc.Zebus.Scan;
using Abc.Zebus.Testing.Extensions;
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
            var invoker = invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<FakeHandlerWithQueueName1>()).ExpectedSingle();

            invoker.DispatchQueueName.ShouldEqual("DispatchQueue1");
        }

        [Test]
        public void should_switch_to_manual_subscription_mode_when_specified()
        {
            var invokerLoader = new SyncMessageHandlerInvokerLoader(new Container());
            var invoker = invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<FakeHandlerWithManualSubscriptionMode>()).ExpectedSingle();

            invoker.ShouldBeSubscribedOnStartup.ShouldBeFalse();
        }

        [Test]
        public void should_switch_to_auto_subscription_mode_when_specified()
        {
            var invokerLoader = new SyncMessageHandlerInvokerLoader(new Container());
            var invoker = invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<FakeRoutableHandlerWithAutoSubscriptionMode>()).ExpectedSingle();

            invoker.ShouldBeSubscribedOnStartup.ShouldBeTrue();
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

        [Routable]
        public class FakeRoutableMessage : IMessage
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
                await Task.CompletedTask;
            }
        }

        [DispatchQueueName("DispatchQueue1")]
        public class FakeHandlerWithQueueName1 : IMessageHandler<FakeMessage>
        {
            public void Handle(FakeMessage message)
            {
            }
        }

        [SubscriptionMode(SubscriptionMode.Manual)]
        public class FakeHandlerWithManualSubscriptionMode : IMessageHandler<FakeMessage>
        {
            public void Handle(FakeMessage message)
            {
            }
        }

        [SubscriptionMode(SubscriptionMode.Auto)]
        public class FakeRoutableHandlerWithAutoSubscriptionMode : IMessageHandler<FakeRoutableMessage>
        {
            public void Handle(FakeRoutableMessage message)
            {
            }
        }
    }
}
