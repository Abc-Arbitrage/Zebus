using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Extensions;

namespace Abc.Zebus.Directory
{
    public partial class PeerDirectoryClient
    {
        private class PeerEntry
        {
            private readonly Dictionary<Subscription, SubscriptionStatus> _subscriptionStatuses = new Dictionary<Subscription, SubscriptionStatus>();
            private readonly ConcurrentDictionary<MessageTypeId, PeerSubscriptionTree> _subscriptionsByMessageType;

            public PeerEntry(PeerDescriptor descriptor, ConcurrentDictionary<MessageTypeId, PeerSubscriptionTree> subscriptionsByMessageType)
            {
                Descriptor = descriptor;
                _subscriptionsByMessageType = subscriptionsByMessageType;
            }

            public PeerDescriptor Descriptor { get; private set; }

            public bool SetSubscriptionEnabled(Subscription subscription, bool enabled, DateTime timestampUtc)
            {
                SubscriptionStatus status;
                if (_subscriptionStatuses.TryGetValue(subscription, out status))
                {
                    if (status.TimestampUtc > timestampUtc || status.Enabled == enabled)
                        return false;

                    status.Enabled = enabled;
                    status.TimestampUtc = timestampUtc;
                }
                else
                {
                    _subscriptionStatuses.Add(subscription, new SubscriptionStatus(enabled, timestampUtc));
                }

                if (enabled)
                    AddToSubscriptionsByMessageType(subscription);
                else
                    RemoveFromSubscriptionsByMessageType(subscription);

                return true;
            }

            public void ReplaceSubscriptions(Subscription[] newSubscriptions)
            {
                ClearDisabledSubscriptions();

                var previousSubscriptions = _subscriptionStatuses.Keys.ToList();
                var newSubscriptionSet = newSubscriptions.ToHashSet();

                foreach (var previousSubscription in previousSubscriptions)
                {
                    if (newSubscriptionSet.Contains(previousSubscription))
                    {
                        newSubscriptionSet.Remove(previousSubscription);
                        continue;
                    }
                    RemoveSubscription(previousSubscription);
                }

                var timestampUtc = Descriptor.TimestampUtc ?? SystemDateTime.UtcNow;
                foreach (var newSubscription in newSubscriptionSet)
                {
                    AddSubscription(newSubscription, timestampUtc);
                }
            }

            private void ClearDisabledSubscriptions()
            {
                var disabledSubscriptions = _subscriptionStatuses.Where(x => !x.Value.Enabled).Select(x => x.Key).ToList();
                _subscriptionStatuses.RemoveRange(disabledSubscriptions);
            }

            private void RemoveSubscription(Subscription subscription)
            {
                _subscriptionStatuses.Remove(subscription);

                RemoveFromSubscriptionsByMessageType(subscription);
            }

            private void AddSubscription(Subscription subscription, DateTime timestampUtc)
            {
                _subscriptionStatuses.Add(subscription, new SubscriptionStatus(true, timestampUtc));

                AddToSubscriptionsByMessageType(subscription);
            }

            public void RemoveSubscriptions()
            {
                foreach (var subscription in _subscriptionStatuses.Where(x => x.Value.Enabled).Select(x => x.Key))
                {
                    RemoveFromSubscriptionsByMessageType(subscription);
                }
                _subscriptionStatuses.Clear();
            }

            private void AddToSubscriptionsByMessageType(Subscription subscription)
            {
                var messageSubscriptions = _subscriptionsByMessageType.GetOrAdd(subscription.MessageTypeId, _ => new PeerSubscriptionTree());
                messageSubscriptions.Add(Descriptor.Peer, subscription.BindingKey);
            }

            private void RemoveFromSubscriptionsByMessageType(Subscription subscription)
            {
                var messageSubscriptions = _subscriptionsByMessageType.GetValueOrDefault(subscription.MessageTypeId);
                if (messageSubscriptions == null)
                    return;

                messageSubscriptions.Remove(Descriptor.Peer, subscription.BindingKey);

                if (messageSubscriptions.IsEmpty)
                    _subscriptionsByMessageType.Remove(subscription.MessageTypeId);
            }

            private class SubscriptionStatus
            {
                public bool Enabled;
                public DateTime TimestampUtc;

                public SubscriptionStatus(bool enabled, DateTime timestampUtc)
                {
                    Enabled = enabled;
                    TimestampUtc = timestampUtc;
                }
            }
        }
    }
}