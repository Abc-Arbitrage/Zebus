using System;

namespace Abc.Zebus.Snapshotting
{
    public abstract class SubscriptionSnapshotGenerator<TMessage> : ISubscriptionHandler
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

        protected abstract TMessage GenerateSnapshot(Subscription subscription, PeerId peer);
    }
}
