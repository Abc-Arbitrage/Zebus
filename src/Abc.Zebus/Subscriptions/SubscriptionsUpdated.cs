using Abc.Zebus.Directory;

namespace Abc.Zebus.Subscriptions
{
    public class SubscriptionsUpdated : IMessage
    {
        public SubscriptionsUpdated(SubscriptionsForType subscriptions, PeerId peerId)
        {
            Subscriptions = subscriptions;
            PeerId = peerId;
        }

        public SubscriptionsUpdated(Subscription subscription, PeerId peerId)
        {
            Subscriptions = new SubscriptionsForType(subscription.MessageTypeId, subscription.BindingKey);
            PeerId = peerId;
        }

        public SubscriptionsForType Subscriptions { get; }
        public PeerId PeerId { get; }
    }
}
