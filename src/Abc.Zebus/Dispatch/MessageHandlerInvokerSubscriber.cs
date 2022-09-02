using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Routing;
using StructureMap;

namespace Abc.Zebus.Dispatch
{
    public class MessageHandlerInvokerSubscriber
    {
        private readonly SubscriptionMode? _subscriptionMode;
        private readonly Type? _startupSubscriberType;

        public MessageHandlerInvokerSubscriber(SubscriptionMode? subscriptionMode, Type? startupSubscriberType)
        {
            _subscriptionMode = subscriptionMode;
            _startupSubscriberType = startupSubscriberType;
        }

        public static MessageHandlerInvokerSubscriber FromAttributes(Type messageHandlerType)
        {
            var subscriptionModeAttribute = (SubscriptionModeAttribute?)Attribute.GetCustomAttribute(messageHandlerType, typeof(SubscriptionModeAttribute));
            if (subscriptionModeAttribute != null)
                return new MessageHandlerInvokerSubscriber(subscriptionModeAttribute.SubscriptionMode, subscriptionModeAttribute.StartupSubscriberType);

            if (Attribute.IsDefined(messageHandlerType, typeof(NoScanAttribute)))
                return new MessageHandlerInvokerSubscriber(SubscriptionMode.Manual, startupSubscriberType: null);

            return new MessageHandlerInvokerSubscriber(subscriptionMode: null, startupSubscriberType: null);
        }

        public IEnumerable<Subscription> GetStartupSubscriptions(Type messageType, MessageTypeId messageTypeId, IContainer container)
        {
            if (_startupSubscriberType != null)
            {
                var startupSubscriber = (IStartupSubscriber)container.GetInstance(_startupSubscriberType);
                return GetSubscriptionsFromSubscriber(startupSubscriber, messageTypeId, messageType);
            }

            var subscriptionMode = _subscriptionMode ?? DefaultSubscriptionMode(messageType);

            return GetSubscriptionsFromMode(subscriptionMode, messageTypeId);
        }

        private static IEnumerable<Subscription> GetSubscriptionsFromSubscriber(IStartupSubscriber startupSubscriber, MessageTypeId messageTypeId, Type messageType)
        {
            return startupSubscriber.GetStartupSubscriptionBindingKeys(messageType)
                                    .Select(x => new Subscription(messageTypeId, x))
                                    .ToArray();
        }

        private static SubscriptionMode DefaultSubscriptionMode(Type messageType)
        {
            return Attribute.IsDefined(messageType, typeof(Routable)) ? SubscriptionMode.Manual : SubscriptionMode.Auto;
        }

        private IEnumerable<Subscription> GetSubscriptionsFromMode(SubscriptionMode subscriptionMode, MessageTypeId messageTypeId)
        {
            return subscriptionMode switch
            {
                SubscriptionMode.Auto   => new[] { new Subscription(messageTypeId) },
                SubscriptionMode.Manual => Array.Empty<Subscription>(),
                _                       => throw new NotSupportedException($"Unsupported subscription mode: {subscriptionMode}"),
            };
        }
    }
}
