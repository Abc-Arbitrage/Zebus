using Abc.Zebus.Directory;

namespace Abc.Zebus.Subscriptions
{
    public class SubscriptionUpdatedMessage : IMessage
    {
        public SubscriptionUpdatedMessage(SubscriptionsForType subscriptions, PeerId peerId)
        {
            Subscriptions = subscriptions;
            PeerId = peerId;
        }

        public SubscriptionUpdatedMessage(Subscription subscription, PeerId peerId)
        {
            Subscriptions = new SubscriptionsForType(subscription.MessageTypeId, subscription.BindingKey);
            PeerId = peerId;
        }

        public SubscriptionsForType Subscriptions { get; }
        public PeerId PeerId { get; }
    }
}
