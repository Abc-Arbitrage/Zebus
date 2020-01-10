using Abc.Zebus.Directory;

namespace Abc.Zebus.SubscriptionHandling
{
    /// <summary>
    /// Extend this class with the message you wish to be notified of subscriptions for.
    /// </summary>
    /// <typeparam name="TMessage">The message that you wish to be notified for.</typeparam>
    public abstract class SubscriptionHandler<TMessage> : IMessageHandler<SubscriptionUpdatedMessage>
        where TMessage : IMessage
    {
        public void Handle(SubscriptionUpdatedMessage message)
        {
            if (message.Subscription.MessageTypeId.GetMessageType() != typeof(TMessage))
                return;

            OnSubscription(message.Subscription, message.PeerId);
        }

        /// <summary>
        /// Called on each new subscription for <see cref="TMessage"/>
        /// </summary>
        /// <param name="subscription">The newly created subscription</param>
        /// <param name="peerId">The peer id of the peer that created the subscription</param>
        protected abstract void OnSubscription(SubscriptionsForType subscription, PeerId peerId);
    }
}
