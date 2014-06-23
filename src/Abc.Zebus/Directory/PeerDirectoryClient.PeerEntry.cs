using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Routing;
using Abc.Zebus.Util.Extensions;

namespace Abc.Zebus.Directory
{
    public partial class PeerDirectoryClient
    {
        private class PeerEntry
        {
            private readonly Dictionary<MessageTypeId, HashSet<BindingKey>> _messageSubscriptions = new Dictionary<MessageTypeId, HashSet<BindingKey>>();
            private readonly ConcurrentDictionary<MessageTypeId, PeerSubscriptionTree> _subscriptionsByMessageType;
            private readonly PeerDescriptor _descriptor;

            public PeerEntry(PeerDescriptor descriptor, ConcurrentDictionary<MessageTypeId, PeerSubscriptionTree> subscriptionsByMessageType)
            {
                _descriptor = descriptor;
                _subscriptionsByMessageType = subscriptionsByMessageType;
            }

            public PeerDescriptor Descriptor { get { return _descriptor; } }

            public Subscription[] GetSubscriptions()
            {
                lock (_messageSubscriptions)
                {
                    return _messageSubscriptions.SelectMany(x => x.Value.Select(bk => new Subscription(x.Key, bk)))
                                                .Distinct()
                                                .ToArray();
                }
            }

            public void SetSubscriptions(IEnumerable<Subscription> subscriptions)
            {
                lock (_messageSubscriptions)
                {
                    var newBindingKeysByMessageType = subscriptions.GroupBy(x => x.MessageTypeId).ToDictionary(g => g.Key, g => g.Select(x => x.BindingKey).ToList());

                    foreach (var bindingKeysForMessageType in _messageSubscriptions)
                    {
                        foreach (var bindingKey in bindingKeysForMessageType.Value)
                        {
                            RemoveFromSubscriptionTree(bindingKeysForMessageType.Key, bindingKey);
                        }
                    }

                    foreach (var newBindingKeyForMessageType in newBindingKeysByMessageType)
                    {
                        SetSubscriptionsForType(newBindingKeyForMessageType.Key, newBindingKeyForMessageType.Value);
                    }
                }
            }

            public void SetSubscriptionsForType(IEnumerable<SubscriptionsForType> subscriptionsForTypes)
            {
                foreach (var subscriptionsForType in subscriptionsForTypes)
                {
                    SetSubscriptionsForType(subscriptionsForType.MessageTypeId, subscriptionsForType.BindingKeys);
                }
            }

            private void SetSubscriptionsForType(MessageTypeId messageTypeId, IEnumerable<BindingKey> bindingKeys)
            {
                var newBindingKeys = bindingKeys.ToHashSet();

                lock (_messageSubscriptions)
                {
                    _messageSubscriptions[messageTypeId] = newBindingKeys;

                    _subscriptionsByMessageType.Remove(messageTypeId);

                    foreach (var newBindingKey in newBindingKeys)
                    {
                        AddToSubscriptionTree(messageTypeId, newBindingKey);
                    }
                }
            }

            public void RemoveSubscriptions()
            {
                lock (_messageSubscriptions)
                {
                    foreach (var messageSubscriptions in _messageSubscriptions)
                    {
                        foreach (var bindingKey in messageSubscriptions.Value)
                        {
                            RemoveFromSubscriptionTree(messageSubscriptions.Key, bindingKey);
                        }
                    }
                    _subscriptionsByMessageType.Clear();
                }
            }

            private void AddToSubscriptionTree(MessageTypeId messageTypeId, BindingKey bindingKey)
            {
                var subscriptionTree = _subscriptionsByMessageType.GetOrAdd(messageTypeId, _ => new PeerSubscriptionTree());
                subscriptionTree.Add(Descriptor.Peer, bindingKey);
            }

            private void RemoveFromSubscriptionTree(MessageTypeId messageTypeId, BindingKey bindingKey)
            {
                var subscriptionTree = _subscriptionsByMessageType.GetValueOrDefault(messageTypeId);
                if (subscriptionTree == null)
                    return;

                subscriptionTree.Remove(Descriptor.Peer, bindingKey);

                if (subscriptionTree.IsEmpty)
                    _subscriptionsByMessageType.Remove(messageTypeId);
            }
        }
    }
}