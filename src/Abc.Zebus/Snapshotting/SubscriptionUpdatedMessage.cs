namespace Abc.Zebus.Snapshotting
{
    public class SubscriptionUpdatedMessage : IMessage
    {
        public SubscriptionUpdatedMessage(Subscription subscription, PeerId peerId)
        {
            Subscription = subscription;
            PeerId = peerId;
        }

        public Subscription Subscription { get; }
        public PeerId PeerId { get; }
    }
}