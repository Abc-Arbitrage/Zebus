using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Abc.Zebus.Directory.Storage
{
    public class MemoryPeerRepository : IPeerRepository
    {
        private class PeerEntry
        {
            public PeerDescriptor PeerDescriptor { get; set; }
            public List<Subscription> DynamicSubscriptions { get; set; }

            public PeerEntry(PeerDescriptor peerDescriptor)
            {
                PeerDescriptor = new PeerDescriptor(peerDescriptor);
                DynamicSubscriptions = new List<Subscription>();
            }

            public PeerDescriptor GetPeerDescriptorWithStaticSubscriptionOnly()
            {
                return new PeerDescriptor(PeerDescriptor)
                {
                    Subscriptions = PeerDescriptor.Subscriptions.ToArray()
                };
            }

            public PeerDescriptor GetMergedPeerDescriptor()
            {
                return new PeerDescriptor(PeerDescriptor)
                {
                    Subscriptions = PeerDescriptor.Subscriptions.Concat(DynamicSubscriptions).Distinct().ToArray()
                };
            }
        }

        private readonly ConcurrentDictionary<PeerId, PeerEntry> _peers = new ConcurrentDictionary<PeerId, PeerEntry>();

        public void AddOrUpdatePeer(PeerDescriptor peerDescriptor)
        {
            var newPeerEntry = new PeerEntry(peerDescriptor);
            _peers.AddOrUpdate(peerDescriptor.PeerId, newPeerEntry, (peerId, existingPeerEntry) => peerDescriptor.TimestampUtc >= existingPeerEntry.PeerDescriptor.TimestampUtc ? newPeerEntry : existingPeerEntry);
        }

        public IEnumerable<PeerDescriptor> GetPeers(bool loadDynamicSubscriptions = true)
        {
            return _peers.Values.Select(entry => loadDynamicSubscriptions ? entry.GetMergedPeerDescriptor() : entry.GetPeerDescriptorWithStaticSubscriptionOnly());
        }

        public bool? IsPersistent(PeerId peerId)
        {
            PeerEntry peerEntry;
            return _peers.TryGetValue(peerId, out peerEntry) ? peerEntry.PeerDescriptor.IsPersistent: (bool?)null;
        }

        public PeerDescriptor Get(PeerId peerId)
        {
            PeerEntry peerEntry;
            return _peers.TryGetValue(peerId, out peerEntry) ? peerEntry.GetMergedPeerDescriptor() : null;
        }

        public void RemovePeer(PeerId peerId)
        {
            PeerEntry unused;
            _peers.TryRemove(peerId, out unused);
        }

        public void SetPeerResponding(PeerId peerId, bool isResponding)
        {
            var peerEntry = GetEntry(peerId);
            if (peerEntry != null)
                peerEntry.PeerDescriptor.Peer.IsResponding = isResponding;
        }
        
        private PeerEntry GetEntry(PeerId peerId)
        {
            PeerEntry peerEntry;
            return _peers.TryGetValue(peerId, out peerEntry) ? peerEntry : null;
        }

        public void AddDynamicSubscriptionsForTypes(PeerId peerId, DateTime timestampUtc, SubscriptionsForType[] subscriptionsForTypes)
        {
            var peerEntry = GetEntry(peerId);
            if (peerEntry == null)
                return;
            if (!(timestampUtc >= peerEntry.PeerDescriptor.TimestampUtc))
                return;

            var subscriptions = subscriptionsForTypes.SelectMany(sub => sub.BindingKeys.Select(binding => new Subscription(sub.MessageTypeId, binding))).ToList();
            peerEntry.DynamicSubscriptions = peerEntry.DynamicSubscriptions.Concat(subscriptions).ToList();
        }

        public void RemoveDynamicSubscriptionsForTypes(PeerId peerId, DateTime timestampUtc, MessageTypeId[] messageTypeIds)
        {
            var peerEntry = GetEntry(peerId);
            if (peerEntry == null)
                return;
            if (timestampUtc >= peerEntry.PeerDescriptor.TimestampUtc)
                peerEntry.DynamicSubscriptions = peerEntry.DynamicSubscriptions.Where(sub => !messageTypeIds.Contains(sub.MessageTypeId)).ToList();
        }

        public void RemoveAllDynamicSubscriptionsForPeer(PeerId peerId, DateTime timestampUtc)
        {
            var peerEntry = GetEntry(peerId);
            if (peerEntry == null)
                return;
            peerEntry.DynamicSubscriptions.Clear();
        }
    }
}