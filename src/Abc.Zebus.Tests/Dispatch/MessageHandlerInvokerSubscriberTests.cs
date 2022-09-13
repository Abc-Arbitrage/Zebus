using System;
using System.Linq;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Routing;
using Abc.Zebus.Testing.Extensions;
using NUnit.Framework;
using StructureMap;

namespace Abc.Zebus.Tests.Dispatch
{
    [TestFixture]
    public class MessageHandlerInvokerSubscriberTests
    {
        [TestCase(typeof(DefaultHandler), typeof(Event), SubscriptionMode.Auto)]
        [TestCase(typeof(DefaultHandler), typeof(RoutableEvent), SubscriptionMode.Manual)]
        [TestCase(typeof(ManualHandler), typeof(Event), SubscriptionMode.Manual)]
        [TestCase(typeof(ManualHandler), typeof(RoutableEvent), SubscriptionMode.Manual)]
        [TestCase(typeof(AutoHandler), typeof(Event), SubscriptionMode.Auto)]
        [TestCase(typeof(AutoHandler), typeof(RoutableEvent), SubscriptionMode.Auto)]
        public void should_get_default_subscription_mode(Type handlerType, Type messageType, SubscriptionMode expectedSubscriptionMode)
        {
            // Arrange
            var messageTypeId = MessageUtil.GetTypeId(messageType);
            var subscriber = MessageHandlerInvokerSubscriber.FromAttributes(handlerType);

            // Act
            var subscriptions = subscriber.GetStartupSubscriptions(messageType, messageTypeId, new Container()).ToList();
            var subscriptionMode = MessageHandlerInvokerSubscriber.GetDefaultSubscriptionMode(handlerType, messageType);

            // Assert
            subscriptionMode.ShouldEqual(expectedSubscriptionMode);
            if (subscriptionMode == SubscriptionMode.Auto)
                subscriptions.ShouldBeEquivalentTo(new Subscription(messageTypeId));
            else
                subscriptions.ShouldBeEmpty();
        }

        [Routable]
        public class RoutableEvent : IEvent
        {
            [RoutingPosition(1)]
            public int Key { get; set; }
        }

        public class Event : IEvent
        {
        }

        public class DefaultHandler : IMessageHandler<RoutableEvent>, IMessageHandler<Event>
        {
            public void Handle(RoutableEvent message)
            {
            }

            public void Handle(Event message)
            {
            }
        }

        [SubscriptionMode(SubscriptionMode.Manual)]
        public class ManualHandler : IMessageHandler<RoutableEvent>, IMessageHandler<Event>
        {
            public void Handle(RoutableEvent message)
            {
            }

            public void Handle(Event message)
            {
            }
        }

        [SubscriptionMode(SubscriptionMode.Auto)]
        public class AutoHandler : IMessageHandler<RoutableEvent>, IMessageHandler<Event>
        {
            public void Handle(RoutableEvent message)
            {
            }

            public void Handle(Event message)
            {
            }
        }
    }
}
