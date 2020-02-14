using System;
using Abc.Zebus.Directory;

namespace Abc.Zebus.Subscriptions
{
    /// <summary>
    /// Extend this class to generate snapshots each time a new subscription is created for <see cref="TMessage"/>
    /// </summary>
    /// <typeparam name="TSnapshotMessage">The type of the snapshot message</typeparam>
    /// <typeparam name="TMessage">The type of the message whose subscriptions will trigger a snapshot</typeparam>
    public abstract class SubscriptionSnapshotGenerator<TSnapshotMessage, TMessage> : SubscriptionHandler<TMessage>
        where TSnapshotMessage : IEvent
        where TMessage : IEvent
    {
        private readonly IBus _bus;

        protected SubscriptionSnapshotGenerator(IBus bus)
        {
            _bus = bus;
        }

        protected override void OnSubscriptionsUpdated(SubscriptionsForType subscriptions, PeerId peerId)
        {
            var snapshot = GenerateSnapshot(subscriptions);
            var internalBus = (IInternalBus)_bus;
            internalBus.Publish(snapshot, peerId);
        }

        /// <summary>
        /// Generate a snapshot for the given subscription
        /// </summary>
        /// <param name="messageSubscription">The subscription of <see cref="TMessage"/></param>
        /// <returns>An instance of the snapshot message</returns>
        protected abstract TSnapshotMessage GenerateSnapshot(SubscriptionsForType messageSubscription);
    }
}
