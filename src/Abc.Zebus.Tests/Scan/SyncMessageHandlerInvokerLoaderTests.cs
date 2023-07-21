﻿using System;
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
            var invoker = invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<FakeHandlerWithQueueName1>()).ExpectedSingle();

            invoker.DispatchQueueName.ShouldEqual("DispatchQueue1");
        }

        [Test]
        public void should_subscribe_to_standard_handler_on_startup()
        {
            var invokerLoader = new SyncMessageHandlerInvokerLoader(new Container());
            var invokers = invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<FakeHandler>()).ToList();

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
            var invoker = invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<FakeRoutableHandler>()).ExpectedSingle();

            invoker.GetStartupSubscriptions().ShouldBeEmpty();
        }

        [Test]
        public void should_subscribe_to_auto_subscribe_routable_message_handler_on_startup()
        {
            var invokerLoader = new SyncMessageHandlerInvokerLoader(new Container());
            var invoker = invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<FakeRoutableMessageWithAutoSubscribeHandler>()).ExpectedSingle();

            invoker.GetStartupSubscriptions().ShouldBeEquivalentTo(Subscription.Any<FakeRoutableMessageWithAutoSubscribe>());
        }

        [Test]
        public void should_subscribe_to_auto_subscribe_routable_message_handler_with_auto_subscription_mode_on_startup()
        {
            var invokerLoader = new SyncMessageHandlerInvokerLoader(new Container());
            var invoker = invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<FakeRoutableMessageWithAutoSubscribeHandler_Auto>()).ExpectedSingle();

            invoker.GetStartupSubscriptions().ShouldBeEquivalentTo(Subscription.Any<FakeRoutableMessageWithAutoSubscribe>());
        }


        [Test]
        public void should_not_subscribe_to_auto_subscribe_routable_message_handler_with_manual_subscription_mode_on_startup()
        {
            var invokerLoader = new SyncMessageHandlerInvokerLoader(new Container());
            var invoker = invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<FakeRoutableMessageWithAutoSubscribeHandler_Manual>()).ExpectedSingle();

            invoker.GetStartupSubscriptions().ShouldBeEmpty();
        }

        [Test]
        public void should_switch_to_manual_subscription_mode_when_specified()
        {
            var invokerLoader = new SyncMessageHandlerInvokerLoader(new Container());
            var invoker = invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<FakeHandlerWithManualSubscriptionMode>()).ExpectedSingle();

            invoker.GetStartupSubscriptions().ShouldBeEmpty();
        }

        [Test]
        public void should_switch_to_auto_subscription_mode_when_specified()
        {
            var invokerLoader = new SyncMessageHandlerInvokerLoader(new Container());
            var invoker = invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<FakeRoutableHandlerWithAutoSubscriptionMode>()).ExpectedSingle();

            var expectedSubscription = Subscription.Any<FakeRoutableMessage>();
            invoker.GetStartupSubscriptions().ShouldBeEquivalentTo(expectedSubscription);
        }

        [Test]
        public void should_use_startup_subscriber()
        {
            var invokerLoader = new SyncMessageHandlerInvokerLoader(new Container());
            var invoker = invokerLoader.LoadMessageHandlerInvokers(TypeSource.FromType<FakeRoutableHandlerWithStartupSubscriber>()).ExpectedSingle();

            var expectedSubscription = new Subscription(MessageUtil.TypeId<FakeRoutableMessage>(), new BindingKey("123"));
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
            [RoutingPosition(1)]
            public string Key;
        }


        [Routable(AutoSubscribe = true)]
        public class FakeRoutableMessageWithAutoSubscribe : IMessage
        {
            [RoutingPosition(1)]
            public string Key;
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

        public class FakeRoutableHandler : IMessageHandler<FakeRoutableMessage>
        {
            public void Handle(FakeRoutableMessage message)
            {
            }
        }

        public class FakeRoutableMessageWithAutoSubscribeHandler : IMessageHandler<FakeRoutableMessageWithAutoSubscribe>
        {
            public void Handle(FakeRoutableMessageWithAutoSubscribe message)
            {
            }
        }

        [SubscriptionMode(SubscriptionMode.Auto)]
        public class FakeRoutableMessageWithAutoSubscribeHandler_Auto : IMessageHandler<FakeRoutableMessageWithAutoSubscribe>
        {
            public void Handle(FakeRoutableMessageWithAutoSubscribe message)
            {
            }
        }

        [SubscriptionMode(SubscriptionMode.Manual)]
        public class FakeRoutableMessageWithAutoSubscribeHandler_Manual : IMessageHandler<FakeRoutableMessageWithAutoSubscribe>
        {
            public void Handle(FakeRoutableMessageWithAutoSubscribe message)
            {
            }
        }


        [SubscriptionMode(typeof(StartupSubscriber))]
        public class FakeRoutableHandlerWithStartupSubscriber : IMessageHandler<FakeRoutableMessage>
        {
            public void Handle(FakeRoutableMessage message)
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
