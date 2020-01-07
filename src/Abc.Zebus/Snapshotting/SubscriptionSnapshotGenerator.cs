using System;
using Abc.Zebus.Directory;

namespace Abc.Zebus.Snapshotting
{
    public abstract class SubscriptionSnapshotGenerator<TSnapshotMessage, TMessage> : ISubscriptionHandler
        where TSnapshotMessage : IEvent
        where TMessage : IEvent
    {
        private readonly IBus _bus;

        protected SubscriptionSnapshotGenerator(IBus bus)
        {
            _bus = bus;
        }

        public void Handle(SubscriptionUpdatedMessage message)
        {
            if (message.Subscription.MessageTypeId.GetMessageType() != typeof(TMessage))
                return;

            var snapshot = GenerateSnapshot(message.Subscription, message.PeerId);
            if (_bus is IInternalBus internalBus)
                internalBus.Publish(snapshot, message.PeerId);
            else
                throw new Exception("The bus is not an internal bus");
        }

        /// <summary>
        /// Generate a snapshot for the given subscription
        /// </summary>
        /// <param name="messageSubscription">The subscription of <see cref="TMessage"/></param>
        /// <param name="peer">The peer that subscribed</param>
        /// <returns>An instance of the snapshot message</returns>
        protected abstract TSnapshotMessage GenerateSnapshot(SubscriptionsForType messageSubscription, PeerId peer);
    }
}
