using System;
using System.Collections.Generic;
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
            var invoker = invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<TestHandler_DispatchQueue1>()).ExpectedSingle();

            invoker.DispatchQueueName.ShouldEqual("DispatchQueue1");
        }

        [Test]
        public void should_subscribe_to_standard_handler_on_startup()
        {
            var invokerLoader = new SyncMessageHandlerInvokerLoader(new Container());
            var invokers = invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<TestHandler>()).ToList();

            invokers.ShouldHaveSize(2);

            foreach (var invoker in invokers)
            {
                invoker.GetStartupSubscriptions().ShouldBeEquivalentTo(new Subscription(invoker.MessageTypeId));
            }
        }

        [Test]
        public void should_not_subscribe_to_routable_handler_on_startup()
        {
            var invokerLoader = new SyncMessageHandlerInvokerLoader(new Container());
            var invoker = invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<TestRoutableHandler>()).ExpectedSingle();

            invoker.GetStartupSubscriptions().ShouldBeEmpty();
        }

        [Test]
        public void should_subscribe_to_auto_subscribe_routable_message_handler_on_startup()
        {
            var invokerLoader = new SyncMessageHandlerInvokerLoader(new Container());
            var invoker = invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<TestAutoSubscribeRoutableHandler>()).ExpectedSingle();

            invoker.GetStartupSubscriptions().ShouldBeEquivalentTo(Subscription.Any<TestAutoSubscribeRoutableMessage>());
        }

        [Test]
        public void should_subscribe_to_auto_subscribe_routable_message_handler_with_auto_subscription_mode_on_startup()
        {
            var invokerLoader = new SyncMessageHandlerInvokerLoader(new Container());
            var invoker = invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<TestAutoSubscribeRoutableHandler_AutoSubscriptionMode>()).ExpectedSingle();

            invoker.GetStartupSubscriptions().ShouldBeEquivalentTo(Subscription.Any<TestAutoSubscribeRoutableMessage>());
        }


        [Test]
        public void should_not_subscribe_to_auto_subscribe_routable_message_handler_with_manual_subscription_mode_on_startup()
        {
            var invokerLoader = new SyncMessageHandlerInvokerLoader(new Container());
            var invoker = invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<TestAutoSubscribeRoutableHandler_ManualSubscriptionMode>()).ExpectedSingle();

            invoker.GetStartupSubscriptions().ShouldBeEmpty();
        }

        [Test]
        public void should_switch_to_manual_subscription_mode_when_specified()
        {
            var invokerLoader = new SyncMessageHandlerInvokerLoader(new Container());
            var invoker = invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<TestHandler_ManualSubscriptionMode>()).ExpectedSingle();

            invoker.GetStartupSubscriptions().ShouldBeEmpty();
        }

        [Test]
        public void should_switch_to_auto_subscription_mode_when_specified()
        {
            var invokerLoader = new SyncMessageHandlerInvokerLoader(new Container());
            var invoker = invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<TestRoutableHandler_AutoSubscriptionMode>()).ExpectedSingle();

            var expectedSubscription = Subscription.Any<TestRoutableMessage>();
            invoker.GetStartupSubscriptions().ShouldBeEquivalentTo(expectedSubscription);
        }

        [Test]
        public void should_use_startup_subscriber()
        {
            var invokerLoader = new SyncMessageHandlerInvokerLoader(new Container());
            var invoker = invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<TestRoutableHandler_StartupSubscriber>()).ExpectedSingle();

            var expectedSubscription = new Subscription(MessageUtil.TypeId<TestRoutableMessage>(), new BindingKey("123"));
            invoker.GetStartupSubscriptions().ShouldBeEquivalentTo(expectedSubscription);
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
            Assert.DoesNotThrow(() => invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<TestHandler>()).ToList());
        }

        public class TestMessage1 : IMessage
        {
        }

        public class TestMessage2 : IMessage
        {
        }

        [Routable]
        public class TestRoutableMessage : IMessage
        {
            [RoutingPosition(1)]
            public string Key;
        }


        [Routable(AutoSubscribe = true)]
        public class TestAutoSubscribeRoutableMessage : IMessage
        {
            [RoutingPosition(1)]
            public string Key;
        }

        public class TestHandler : IMessageHandler<TestMessage1>, IMessageHandler<TestMessage2>
        {
            public void Handle(TestMessage1 message)
            {
                throw new NotImplementedException();
            }

            public void Handle(TestMessage2 message)
            {
                throw new NotImplementedException();
            }
        }

        public class WrongAsyncHandler : IMessageHandler<TestMessage1>
        {
            public async void Handle(TestMessage1 message)
            {
                await Task.CompletedTask;
            }
        }

        [DispatchQueueName("DispatchQueue1")]
        public class TestHandler_DispatchQueue1 : IMessageHandler<TestMessage1>
        {
            public void Handle(TestMessage1 message)
            {
            }
        }

        [SubscriptionMode(SubscriptionMode.Manual)]
        public class TestHandler_ManualSubscriptionMode : IMessageHandler<TestMessage1>
        {
            public void Handle(TestMessage1 message)
            {
            }
        }

        [SubscriptionMode(SubscriptionMode.Auto)]
        public class TestRoutableHandler_AutoSubscriptionMode : IMessageHandler<TestRoutableMessage>
        {
            public void Handle(TestRoutableMessage message)
            {
            }
        }

        public class TestRoutableHandler : IMessageHandler<TestRoutableMessage>
        {
            public void Handle(TestRoutableMessage message)
            {
            }
        }

        public class TestAutoSubscribeRoutableHandler : IMessageHandler<TestAutoSubscribeRoutableMessage>
        {
            public void Handle(TestAutoSubscribeRoutableMessage message)
            {
            }
        }

        [SubscriptionMode(SubscriptionMode.Auto)]
        public class TestAutoSubscribeRoutableHandler_AutoSubscriptionMode : IMessageHandler<TestAutoSubscribeRoutableMessage>
        {
            public void Handle(TestAutoSubscribeRoutableMessage message)
            {
            }
        }

        [SubscriptionMode(SubscriptionMode.Manual)]
        public class TestAutoSubscribeRoutableHandler_ManualSubscriptionMode : IMessageHandler<TestAutoSubscribeRoutableMessage>
        {
            public void Handle(TestAutoSubscribeRoutableMessage message)
            {
            }
        }


        [SubscriptionMode(typeof(StartupSubscriber))]
        public class TestRoutableHandler_StartupSubscriber : IMessageHandler<TestRoutableMessage>
        {
            public void Handle(TestRoutableMessage message)
            {
            }

            public class StartupSubscriber : IStartupSubscriber
            {
                public IEnumerable<BindingKey> GetStartupSubscriptionBindingKeys(Type messageType)
                {
                    yield return new BindingKey("123");
                }
            }
        }
    }
}
