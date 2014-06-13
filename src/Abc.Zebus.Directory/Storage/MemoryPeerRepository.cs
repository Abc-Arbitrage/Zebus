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

        public IEnumerable<PeerDescriptor> GetPeers()
        {
            return _peers.Values.Select(entry => entry.GetMergedPeerDescriptor());
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

        public void AddDynamicSubscriptions(PeerId peerId, DateTime timestampUtc, Subscription[] subscriptions)
        {
            var peerEntry = GetEntry(peerId);
            if (peerEntry == null)
                return;
            if (timestampUtc >= peerEntry.PeerDescriptor.TimestampUtc)
                peerEntry.DynamicSubscriptions = peerEntry.DynamicSubscriptions.Concat(subscriptions).ToList();
        }

        public void RemoveDynamicSubscriptions(PeerId peerId, DateTime timestampUtc, Subscription[] subscriptions)
        {
            var peerEntry = GetEntry(peerId);
            if (peerEntry == null)
                return;
            if (timestampUtc >= peerEntry.PeerDescriptor.TimestampUtc)
                peerEntry.DynamicSubscriptions = peerEntry.DynamicSubscriptions.Except(subscriptions).ToList();
        }
    }
}