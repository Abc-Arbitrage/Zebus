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

        public static bool SetPeerRespondingState(this IPeerRepository repository, PeerId peerId, bool isResponding, DateTime timestampUtc)
        {
            var peer = repository.Get(peerId);
            if (peer == null || peer.TimestampUtc > timestampUtc)
                return false;

            peer.Peer.IsResponding = isResponding;
            peer.TimestampUtc = timestampUtc;
            repository.AddOrUpdatePeer(peer);

            return true;
        }

        public static PeerDescriptor UpdatePeerSubscriptions(this IPeerRepository repository, PeerId peerId, Subscription[] subscriptions, DateTime? timestampUtc)
        {
            var peerDescriptor = repository.Get(peerId);
            if (peerDescriptor == null)
                throw new InvalidOperationException(string.Format("The specified Peer ({0}) does not exist.", peerId));

            if (peerDescriptor.TimestampUtc > timestampUtc)
                return null;

            peerDescriptor.TimestampUtc = timestampUtc;
            peerDescriptor.Subscriptions = subscriptions;
            repository.AddOrUpdatePeer(peerDescriptor);

            return peerDescriptor;
        }
    }
}