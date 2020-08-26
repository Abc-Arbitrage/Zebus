using System.Collections.Generic;
using Abc.Zebus.Directory;

namespace Abc.Zebus.Subscriptions
{
    /// <summary>
    /// Extend this class to generate multiple snapshots each time a new subscription is created for <see cref="TMessage"/>
    /// </summary>
    /// <typeparam name="TSnapshotMessage">The type of the snapshot message</typeparam>
    /// <typeparam name="TMessage">The type of the message whose subscriptions will trigger a snapshot</typeparam>
    /// <remarks>
    /// Notes:
    /// - The <see cref="GenerateSnapshots"/> method will be invoked after the directory state is updated.
    /// - Messages can be sent to the subscribing peer before the snapshot.
    /// - The subscribing peer should properly handle (discard) messages received before the snapshot.
    /// </remarks>
    public abstract class SubscriptionMultipleSnapshotGenerator<TSnapshotMessage, TMessage> : SubscriptionHandler<TMessage>
        where TSnapshotMessage : IEvent
        where TMessage : IEvent
    {
        private readonly IBus _bus;

        protected SubscriptionMultipleSnapshotGenerator(IBus bus)
        {
            _bus = bus;
        }

        protected sealed override void OnSubscriptionsUpdated(SubscriptionsForType subscriptions, PeerId peerId)
        {
            var snapshots = GenerateSnapshots(subscriptions);
            var internalBus = (IInternalBus)_bus;
            foreach (var snapshotMessage in snapshots)
            {
                internalBus.Publish(snapshotMessage, peerId);
            }
        }

        /// <summary>
        /// Generate multiple snapshots for the given subscription
        /// </summary>
        /// <param name="messageSubscription">The subscription of <see cref="TMessage"/></param>
        /// <returns>Instances of the snapshot message</returns>
        protected abstract IEnumerable<TSnapshotMessage> GenerateSnapshots(SubscriptionsForType messageSubscription);
    }
}
