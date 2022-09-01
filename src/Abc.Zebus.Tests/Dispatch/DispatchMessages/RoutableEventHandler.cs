using System;
using System.Collections.Generic;
using Abc.Zebus.Routing;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    [SubscriptionMode(typeof(Subscriber))]
    public class RoutableEventHandler : IMessageHandler<RoutableEvent>
    {
        public void Handle(RoutableEvent message)
        {
        }

        public class Subscriber : IStartupSubscriber
        {
            public List<Type> MessageTypes { get; } = new();
            public List<BindingKey> BindingKeys { get; } = new();

            public IEnumerable<BindingKey> GetStartupSubscriptionBindingKeys(Type messageType)
            {
                MessageTypes.Add(messageType);

                return BindingKeys;
            }
        }
    }
}
