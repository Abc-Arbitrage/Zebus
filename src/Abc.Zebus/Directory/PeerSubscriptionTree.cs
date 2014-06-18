using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Routing;

namespace Abc.Zebus.Directory
{
    public class PeerSubscriptionTree
    {
        private readonly SubscriptionNode _rootNode = new SubscriptionNode(0);
        private List<Peer> _peersMatchingAllMessages = new List<Peer>();

        public bool IsEmpty
        {
            get { return _rootNode.IsEmpty && _peersMatchingAllMessages.Count == 0; }
        }

        public void Add(Peer peer, BindingKey subscription)
        {
            UpdatePeerSubscription(peer, subscription, UpdateAction.Add);
        }

        public void Remove(Peer peer, BindingKey subscription)
        {
            UpdatePeerSubscription(peer, subscription, UpdateAction.Remove);
        }

        public IList<Peer> GetPeers(BindingKey routingKey)
        {
            var peerCollector = new PeerCollector(_peersMatchingAllMessages);

            _rootNode.Accept(peerCollector, routingKey);

            return peerCollector.GetPeers();
        }

        private void UpdatePeerSubscription(Peer peer, BindingKey subscription, UpdateAction action)
        {
            if (subscription.IsEmpty)
                UpdatePeersMatchingAllMessages(peer, action);
            else
                _rootNode.Update(peer, subscription, action);
        }

        private void UpdatePeersMatchingAllMessages(Peer peer, UpdateAction action)
        {
            UpdateList(ref _peersMatchingAllMessages, peer, action);
        }

        private static void UpdateList(ref List<Peer> peers, Peer peer, UpdateAction action)
        {
            var newPeers = new List<Peer>(peers.Capacity);
            newPeers.AddRange(peers.Where(x => x.Id != peer.Id));

            if (action == UpdateAction.Add)
                newPeers.Add(peer);

            peers = newPeers;
        }

        private class PeerCollector
        {
            private readonly Dictionary<PeerId, Peer> _collectedPeers = new Dictionary<PeerId, Peer>();
            private readonly List<Peer> _initialPeers;

            public PeerCollector(List<Peer> initialPeers)
            {
                _initialPeers = initialPeers;
            }

            public void Offer(IEnumerable<Peer> peers)
            {
                foreach (var peer in peers)
                {
                    _collectedPeers[peer.Id] = peer;
                }
            }

            public List<Peer> GetPeers()
            {
                if (_collectedPeers.Count == 0)
                    return _initialPeers;

                Offer(_initialPeers);

                return _collectedPeers.Values.ToList();
            }
        }

        private class SubscriptionNode
        {
            private readonly int _nextPartIndex;
            private readonly bool _matchesAll;
            private Dictionary<string, SubscriptionNode> _childrenNodes = new Dictionary<string, SubscriptionNode>();
            private List<Peer> _peers = new List<Peer>();
            private SubscriptionNode _sharpNode;
            private SubscriptionNode _starNode;

            public SubscriptionNode(int nextPartIndex, bool matchesAll = false)
            {
                _nextPartIndex = nextPartIndex;
                _matchesAll = matchesAll;
            }

            public bool IsEmpty
            {
                get { return _peers.Count == 0 && (_sharpNode == null || _sharpNode.IsEmpty) && (_starNode == null || _starNode.IsEmpty) && _childrenNodes.All(x => x.Value.IsEmpty); }
            }

            public void Accept(PeerCollector peerCollector, BindingKey routingKey)
            {
                if (IsLeaf(routingKey) || _matchesAll)
                {
                    peerCollector.Offer(_peers);
                    return;
                }

                if (_sharpNode != null)
                    _sharpNode.Accept(peerCollector, routingKey);

                if (_starNode != null)
                    _starNode.Accept(peerCollector, routingKey);

                var nextPart = routingKey.GetPart(_nextPartIndex);
                if (nextPart == null)
                    return;

                SubscriptionNode childNode;
                if (_childrenNodes.TryGetValue(nextPart, out childNode))
                    childNode.Accept(peerCollector, routingKey);
            }

            public void Update(Peer peer, BindingKey subscription, UpdateAction action)
            {
                if (IsLeaf(subscription))
                {
                    UpdateList(peer, action);
                    return;
                }

                var nextPart = subscription.GetPart(_nextPartIndex);

                if (nextPart == "#" || nextPart == null)
                {
                    var sharpNode = GetOrCreateSharpNode();
                    sharpNode.Update(peer, subscription, action);
                    return;
                }

                if (nextPart == "*")
                {
                    var starNode = GetOrCreateStarNode();
                    starNode.Update(peer, subscription, action);
                    return;
                }

                var childNode = GetOrAddChildNode(nextPart);
                childNode.Update(peer, subscription, action);
            }

            private bool IsLeaf(BindingKey bindingKey)
            {
                if (_nextPartIndex == 0)
                    return false;

                if (bindingKey.IsEmpty)
                    return _nextPartIndex == 1;

                return _nextPartIndex == bindingKey.PartCount;
            }

            private SubscriptionNode CreateChildNode(bool matchesAll = false)
            {
                return new SubscriptionNode(_nextPartIndex + 1, matchesAll);
            }

            private SubscriptionNode GetOrAddChildNode(string part)
            {
                SubscriptionNode child;
                if (!_childrenNodes.TryGetValue(part, out child))
                {
                    child = CreateChildNode();
                    var newChildren = _childrenNodes.ToDictionary(x => x.Key, x => x.Value);
                    newChildren.Add(part, child);
                    _childrenNodes = newChildren;
                }
                return child;
            }

            private SubscriptionNode GetOrCreateSharpNode()
            {
                return _sharpNode ?? (_sharpNode = CreateChildNode(true));
            }

            private SubscriptionNode GetOrCreateStarNode()
            {
                return _starNode ?? (_starNode = CreateChildNode());
            }

            private void UpdateList(Peer peer, UpdateAction action)
            {
                PeerSubscriptionTree.UpdateList(ref _peers, peer, action);
            }
        }

        private enum UpdateAction
        {
            Add,
            Remove,
        }
    }
}