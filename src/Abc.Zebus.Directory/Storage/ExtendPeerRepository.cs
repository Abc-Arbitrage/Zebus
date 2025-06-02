using System;

namespace Abc.Zebus.Directory.Storage
{
    public static class ExtendPeerRepository
    {
        public static bool SetPeerDown(this IPeerRepository repository, PeerId peerId, DateTime timestampUtc)
        {
            var peer = repository.Get(peerId);
            if (peer == null || peer.TimestampUtc > timestampUtc)
                return false;

            peer.Peer.IsUp = false;
            peer.Peer.IsResponding = false;
            peer.TimestampUtc = timestampUtc;
            repository.AddOrUpdatePeer(peer);

            return true;
        }

        public static bool SetPeerRespondingState(this IPeerRepository repository, PeerId peerId, bool isResponding, out DateTime timestampUtc)
        {
            var peer = repository.Get(peerId);
            if (peer == null || peer.TimestampUtc == null)
            {
                timestampUtc = default;
                return false;
            }

            timestampUtc = peer.TimestampUtc.Value.AddMilliseconds(1);
            repository.SetPeerResponding(peer.PeerId, isResponding, peer.TimestampUtc.Value.AddMilliseconds(1));

            return true;
        }

        public static bool SetPeerRespondingState(this IPeerRepository repository, PeerId peerId, bool isResponding, DateTime timestampUtc)
        {
            var peer = repository.Get(peerId);
            if (peer == null || peer.TimestampUtc == null || peer.TimestampUtc > timestampUtc)
                return false;

            repository.SetPeerResponding(peer.PeerId, isResponding, timestampUtc);

            return true;
        }

        public static PeerDescriptor? UpdatePeerSubscriptions(this IPeerRepository repository, PeerId peerId, Subscription[] subscriptions, DateTime? timestampUtc)
        {
            var peerDescriptor = repository.Get(peerId);
            if (peerDescriptor == null)
                throw new InvalidOperationException($"The specified Peer ({peerId}) does not exist.");

            if (peerDescriptor.TimestampUtc > timestampUtc)
                return null;

            peerDescriptor.TimestampUtc = timestampUtc;
            peerDescriptor.Subscriptions = subscriptions;
            repository.AddOrUpdatePeer(peerDescriptor);

            return peerDescriptor;
        }
    }
}
