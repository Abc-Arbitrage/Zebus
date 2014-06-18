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
            // TODO LVK: replace with commented dictionnary
            //private readonly Dictionary<MessageTypeId, HashSet<BindingKey>> _messageSubscriptions = new Dictionary<MessageTypeId, HashSet<BindingKey>>();

            private readonly Dictionary<MessageTypeId, MessageSubscriptions> _messageSubscriptions = new Dictionary<MessageTypeId, MessageSubscriptions>();
            private readonly ConcurrentDictionary<MessageTypeId, PeerSubscriptionTree> _subscriptionsByMessageType;
            private readonly PeerDescriptor _descriptor;

            public PeerEntry(PeerDescriptor descriptor, ConcurrentDictionary<MessageTypeId, PeerSubscriptionTree> subscriptionsByMessageType)
            {
                _descriptor = descriptor;
                _subscriptionsByMessageType = subscriptionsByMessageType;
            }

            public PeerDescriptor Descriptor
            {
                get { return _descriptor; }
            }

            public Subscription[] GetSubscriptions()
            {
                var subscriptions = new HashSet<Subscription>();
                foreach (var messageSubscriptions in _messageSubscriptions)
                {
                    foreach (var bindingKey in messageSubscriptions.Value.DynamicBindingKeys)
                    {
                        subscriptions.Add(new Subscription(messageSubscriptions.Key, bindingKey));
                    }
                    foreach (var bindingKey in messageSubscriptions.Value.StaticBindingKeys)
                    {
                        subscriptions.Add(new Subscription(messageSubscriptions.Key, bindingKey));
                    }
                }
                return subscriptions.ToArray();
            }

            public void SetStaticSubscriptions(IEnumerable<Subscription> staticSubscriptions)
            {
                var newBindingKeysByMessageType = staticSubscriptions.GroupBy(x => x.MessageTypeId).ToDictionary(g => g.Key, g => g.Select(x => x.BindingKey).ToList());

                foreach (var messageSubscriptions in _messageSubscriptions)
                {
                    var newBindingKeys = newBindingKeysByMessageType.GetValueOrDefault(messageSubscriptions.Key);

                    foreach (var existingBindingKey in messageSubscriptions.Value.StaticBindingKeys.ToList())
                    {
                        if (newBindingKeys != null && newBindingKeys.Contains(existingBindingKey))
                            continue;

                        messageSubscriptions.Value.StaticBindingKeys.Remove(existingBindingKey);

                        if (messageSubscriptions.Value.DynamicBindingKeys.Contains(existingBindingKey))
                            continue;

                        RemoveFromSubscriptionTree(messageSubscriptions.Key, existingBindingKey);
                    }
                }

                foreach (var newBindingKeys in newBindingKeysByMessageType)
                {
                    var messageSubscriptions = _messageSubscriptions.GetValueOrAdd(newBindingKeys.Key, () => new MessageSubscriptions());
                    foreach (var newBindingKey in newBindingKeys.Value)
                    {
                        if (!messageSubscriptions.StaticBindingKeys.Add(newBindingKey))
                            continue;

                        if (messageSubscriptions.DynamicBindingKeys.Contains(newBindingKey))
                            continue;

                        AddToSubscriptionTree(newBindingKeys.Key, newBindingKey);
                    }
                }
            }

            public void SetDynamicSubscriptions(IEnumerable<Subscription> subscriptions)
            {
                var subscriptionsByType = new Dictionary<MessageTypeId, IEnumerable<BindingKey>>();
                foreach (var messageTypeId in _messageSubscriptions.Keys)
                {
                    subscriptionsByType.Add(messageTypeId, Enumerable.Empty<BindingKey>());
                }
                foreach (var messageTypeSubscriptions in subscriptions.GroupBy(x => x.MessageTypeId))
                {
                    subscriptionsByType[messageTypeSubscriptions.Key] = messageTypeSubscriptions.Select(x => x.BindingKey);
                }
                foreach (var messageTypeSubscriptions in subscriptionsByType)
                {
                    SetDynamicSubscriptionsForType(messageTypeSubscriptions.Key, messageTypeSubscriptions.Value);
                }
            }

            public void SetDynamicSubscriptionsForType(IEnumerable<SubscriptionsForType> subscriptionsForTypes)
            {
                foreach (var subscriptionsForType in subscriptionsForTypes)
                {
                    SetDynamicSubscriptionsForType(subscriptionsForType.MessageTypeId, subscriptionsForType.BindingKeys);
                }
            }

            public void SetDynamicSubscriptionsForType(MessageTypeId messageTypeId, IEnumerable<BindingKey> bindingKeys)
            {
                var messageSubscriptions = _messageSubscriptions.GetValueOrAdd(messageTypeId, () => new MessageSubscriptions());
                var newBindingKeys = bindingKeys.ToHashSet();

                foreach (var previousBindingKey in messageSubscriptions.DynamicBindingKeys.ToList())
                {
                    if (newBindingKeys.Remove(previousBindingKey))
                        continue;

                    messageSubscriptions.DynamicBindingKeys.Remove(previousBindingKey);

                    if (messageSubscriptions.StaticBindingKeys.Contains(previousBindingKey))
                        continue;

                    RemoveFromSubscriptionTree(messageTypeId, previousBindingKey);
                }

                foreach (var newBindingKey in newBindingKeys)
                {
                    messageSubscriptions.DynamicBindingKeys.Add(newBindingKey);

                    if (messageSubscriptions.StaticBindingKeys.Contains(newBindingKey))
                        continue;

                    AddToSubscriptionTree(messageTypeId, newBindingKey);
                }
            }

            public void RemoveSubscriptions()
            {
                foreach (var messageSubscriptions in _messageSubscriptions)
                {
                    var subscriptionTree = _subscriptionsByMessageType.GetValueOrDefault(messageSubscriptions.Key);
                    foreach (var bindingKey in messageSubscriptions.Value.GetAll())
                    {
                        RemoveFromSubscriptionTree(messageSubscriptions.Key, bindingKey);
                    }
                }
                _subscriptionsByMessageType.Clear();
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

            public class MessageSubscriptions
            {
                public readonly HashSet<BindingKey> StaticBindingKeys = new HashSet<BindingKey>();
                public readonly HashSet<BindingKey> DynamicBindingKeys = new HashSet<BindingKey>();

                public IEnumerable<BindingKey> GetAll()
                {
                    foreach (var dynamicSubscription in DynamicBindingKeys)
                    {
                        yield return dynamicSubscription;
                    }
                    foreach (var staticSubscription in StaticBindingKeys)
                    {
                        if (!DynamicBindingKeys.Contains(staticSubscription))
                            yield return staticSubscription;
                    }
                }
            }
        }
    }
}