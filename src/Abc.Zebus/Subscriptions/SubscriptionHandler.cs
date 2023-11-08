using Abc.Zebus.Directory;

namespace Abc.Zebus.Subscriptions;

/// <summary>
/// Extend this class with the message you wish to be notified of subscriptions for.
/// </summary>
/// <typeparam name="TMessage">The message that you wish to be notified for.</typeparam>
public abstract class SubscriptionHandler<TMessage> : IMessageHandler<SubscriptionsUpdated>
    where TMessage : IMessage
{
    public void Handle(SubscriptionsUpdated message)
    {
        if (message.Subscriptions.MessageTypeId.GetMessageType() != typeof(TMessage))
            return;

        OnSubscriptionsUpdated(message.Subscriptions, message.PeerId);
    }

    /// <summary>
    /// Called on each new subscription for <see cref="TMessage"/>
    /// </summary>
    /// <param name="subscriptions">The newly created subscription</param>
    /// <param name="peerId">The peer id of the peer that created the subscription</param>
    protected abstract void OnSubscriptionsUpdated(SubscriptionsForType subscriptions, PeerId peerId);
}
