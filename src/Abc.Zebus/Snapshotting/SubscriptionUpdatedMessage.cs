using Abc.Zebus.Directory;

namespace Abc.Zebus.Snapshotting
{
    public class SubscriptionUpdatedMessage : IMessage
    {
        public SubscriptionUpdatedMessage(SubscriptionsForType subscription, PeerId peerId)
        {
            Subscription = subscription;
            PeerId = peerId;
        }
        public SubscriptionUpdatedMessage(Subscription subscription, PeerId peerId)
        {
            Subscription = new SubscriptionsForType(subscription.MessageTypeId, subscription.BindingKey);
            PeerId = peerId;
        }


        public SubscriptionsForType Subscription { get; }
        public PeerId PeerId { get; }
    }
}
