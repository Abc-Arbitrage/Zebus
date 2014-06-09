using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Abc.Zebus.Routing;
using Abc.Zebus.Util.Extensions;

namespace Abc.Zebus.Tests.Directory
{
    /// <summary>
    /// Old container for peer subscriptions, now replaced by PeerSubscriptionTree.
    /// It is kept for performance comparison purposes.
    /// </summary>
    internal class PeerSubscriptionList
    {
        private List<PeerSubscription> _dynamicPeerSubscriptions = new List<PeerSubscription>();
        private List<Peer> _peersHandlingAllMessages = new List<Peer>();

        public bool IsEmpty
        {
            get { return _peersHandlingAllMessages.Count == 0 && _dynamicPeerSubscriptions.Count == 0; }
        }

        public void Add(Peer peer, Subscription subscription)
        {
            UpdateCore(peer, subscription, true);
        }

        public IList<Peer> GetPeers(BindingKey routingKey)
        {
            if (_dynamicPeerSubscriptions.Count == 0)
                return _peersHandlingAllMessages;

            return _peersHandlingAllMessages
                .Concat(_dynamicPeerSubscriptions.Where(x => x.Subscription.Matches(routingKey)).Select(i => i.Peer))
                .DistinctBy(i => i.Id)
                .ToList();
        }

        public void Remove(Peer peer, Subscription subscription)
        {
            UpdateCore(peer, subscription, false);
        }

        private void UpdateCore(Peer peer, Subscription subscription, bool isAddOrUpdate)
        {
            if (subscription.IsMatchingAllMessages)
            {
                var list = _peersHandlingAllMessages
                    .Where(i => i.Id != peer.Id)
                    .ToList();

                if (isAddOrUpdate)
                    list.Add(peer);

                _peersHandlingAllMessages = list;
            }
            else
            {
                var list = _dynamicPeerSubscriptions
                    .Where(item => item.Peer.Id != peer.Id || !Equals(item.Subscription, subscription))
                    .ToList();

                if (isAddOrUpdate)
                    list.Add(new PeerSubscription(peer, subscription));

                Interlocked.Exchange(ref _dynamicPeerSubscriptions, list);
            }
        }

        private class PeerSubscription
        {
            public readonly Peer Peer;
            public readonly Subscription Subscription;

            public PeerSubscription(Peer peer, Subscription subscription)
            {
                Peer = peer;
                Subscription = subscription;
            }
        }
    }
}