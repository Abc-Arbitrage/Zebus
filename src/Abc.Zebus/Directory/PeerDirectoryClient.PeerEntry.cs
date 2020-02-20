using System;
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
            private readonly Dictionary<MessageTypeId, MessageTypeEntry> _peerSubscriptionsByMessageType = new Dictionary<MessageTypeId, MessageTypeEntry>();
            private readonly ConcurrentDictionary<MessageTypeId, PeerSubscriptionTree> _globalSubscriptionsIndex;

            public PeerEntry(PeerDescriptor descriptor, ConcurrentDictionary<MessageTypeId, PeerSubscriptionTree> globalSubscriptionsIndex)
            {
                Peer = new Peer(descriptor.Peer);
                IsPersistent = descriptor.IsPersistent;
                TimestampUtc = descriptor.TimestampUtc ?? DateTime.UtcNow;
                HasDebuggerAttached = descriptor.HasDebuggerAttached;

                _globalSubscriptionsIndex = globalSubscriptionsIndex;
            }

            public Peer Peer { get; }
            public bool IsPersistent { get; set; }
            public DateTime TimestampUtc { get; set; }
            public bool HasDebuggerAttached { get; set; }

            public PeerDescriptor ToPeerDescriptor()
            {
                lock (_peerSubscriptionsByMessageType)
                {
                    var subscriptions = _peerSubscriptionsByMessageType.SelectMany(x => x.Value.BindingKeys.Select(bk => new Subscription(x.Key, bk)))
                                                             .Distinct()
                                                             .ToArray();

                    return new PeerDescriptor(Peer.Id, Peer.EndPoint, IsPersistent, Peer.IsUp, Peer.IsResponding, TimestampUtc, subscriptions);
                }
            }

            public void SetSubscriptions(IEnumerable<Subscription> subscriptions, DateTime? timestampUtc)
            {
                lock (_peerSubscriptionsByMessageType)
                {
                    var newBindingKeysByMessageType = subscriptions.GroupBy(x => x.MessageTypeId).ToDictionary(g => g.Key, g => g.Select(x => x.BindingKey));

                    foreach (var messageSubscriptions in _peerSubscriptionsByMessageType)
                    {
                        if (!newBindingKeysByMessageType.ContainsKey(messageSubscriptions.Key))
                            SetSubscriptionsForType(messageSubscriptions.Key, Enumerable.Empty<BindingKey>(), timestampUtc);
                    }

                    foreach (var newBindingKeys in newBindingKeysByMessageType)
                    {
                        SetSubscriptionsForType(newBindingKeys.Key, newBindingKeys.Value, timestampUtc);
                    }
                }
            }

            public void SetSubscriptionsForType(IEnumerable<SubscriptionsForType> subscriptionsForTypes, DateTime? timestampUtc)
            {
                lock (_peerSubscriptionsByMessageType)
                {
                    foreach (var subscriptionsForType in subscriptionsForTypes)
                    {
                        var messageTypeId = subscriptionsForType.MessageTypeId;
                        var bindingKeys = subscriptionsForType.BindingKeys ?? Enumerable.Empty<BindingKey>();
                        SetSubscriptionsForType(messageTypeId, bindingKeys, timestampUtc);
                    }
                }
            }

            private void SetSubscriptionsForType(MessageTypeId messageTypeId, IEnumerable<BindingKey> bindingKeys, DateTime? timestampUtc)
            {
                var newBindingKeys = bindingKeys.ToHashSet();

                var messageTypeEntry = _peerSubscriptionsByMessageType.GetValueOrAdd(messageTypeId, MessageTypeEntry.Create);
                if (messageTypeEntry.TimestampUtc > timestampUtc)
                    return;

                messageTypeEntry.TimestampUtc = timestampUtc;

                foreach (var previousBindingKey in messageTypeEntry.BindingKeys.ToList())
                {
                    if (newBindingKeys.Remove(previousBindingKey))
                        continue;

                    messageTypeEntry.BindingKeys.Remove(previousBindingKey);

                    RemoveFromGlobalSubscriptionsIndex(messageTypeId, previousBindingKey);
                }

                foreach (var newBindingKey in newBindingKeys)
                {
                    if (!messageTypeEntry.BindingKeys.Add(newBindingKey))
                        continue;

                    AddToGlobalSubscriptionsIndex(messageTypeId, newBindingKey);
                }
            }

            public void RemoveSubscriptions()
            {
                lock (_peerSubscriptionsByMessageType)
                {
                    foreach (var messageSubscriptions in _peerSubscriptionsByMessageType)
                    {
                        foreach (var bindingKey in messageSubscriptions.Value.BindingKeys)
                        {
                            RemoveFromGlobalSubscriptionsIndex(messageSubscriptions.Key, bindingKey);
                        }
                    }
                    _peerSubscriptionsByMessageType.Clear();
                }
            }

            private void AddToGlobalSubscriptionsIndex(MessageTypeId messageTypeId, BindingKey bindingKey)
            {
                var subscriptionTree = _globalSubscriptionsIndex.GetOrAdd(messageTypeId, _ => new PeerSubscriptionTree());
                subscriptionTree.Add(Peer, bindingKey);
            }

            private void RemoveFromGlobalSubscriptionsIndex(MessageTypeId messageTypeId, BindingKey bindingKey)
            {
                var subscriptionTree = _globalSubscriptionsIndex.GetValueOrDefault(messageTypeId);
                if (subscriptionTree == null)
                    return;

                subscriptionTree.Remove(Peer, bindingKey);

                if (subscriptionTree.IsEmpty)
                    _globalSubscriptionsIndex.Remove(messageTypeId);
            }

            private class MessageTypeEntry
            {
                public static readonly Func<MessageTypeEntry> Create = () => new MessageTypeEntry();

                public readonly HashSet<BindingKey> BindingKeys = new HashSet<BindingKey>();
                public DateTime? TimestampUtc;
            }
        }
    }
}
