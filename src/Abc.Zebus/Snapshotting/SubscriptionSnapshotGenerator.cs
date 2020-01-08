using System;
using Abc.Zebus.Directory;

namespace Abc.Zebus.Snapshotting
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

        protected override void OnSubscription(SubscriptionsForType subscription, PeerId peerId)
        {
            var snapshot = GenerateSnapshot(subscription, peerId);
            if (_bus is IInternalBus internalBus)
                internalBus.Publish(snapshot, peerId);
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
