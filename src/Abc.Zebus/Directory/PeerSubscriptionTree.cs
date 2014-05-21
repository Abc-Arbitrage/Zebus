using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Routing;

namespace Abc.Zebus.Directory
{
    public class PeerSubscriptionTree
    {
        private readonly SubscriptionNode _rootNode = new SubscriptionNode(0);

        public bool IsEmpty
        {
            get { return _rootNode.IsEmpty; }
        }

        public void Add(Peer peer, Subscription subscription)
        {
            _rootNode.Update(peer, subscription, true);
        }

        public IList<Peer> GetPeers(BindingKey bindingKey)
        {
            var peerCollector = new PeerCollector();

            _rootNode.Accept(peerCollector, bindingKey);

            return (List<Peer>)peerCollector;
        }

        public void Remove(Peer peer, Subscription subscription)
        {
            _rootNode.Update(peer, subscription, false);
        }

        private class PeerCollector
        {
            private readonly HashSet<PeerId> _peerIds = new HashSet<PeerId>();
            private readonly List<Peer> _peers = new List<Peer>();

            public void Offer(IEnumerable<Peer> peers)
            {
                foreach (var peer in peers.Where(x => _peerIds.Add(x.Id)))
                {
                    _peers.Add(peer);
                }
            }

            public static implicit operator List<Peer>(PeerCollector collector)
            {
                return collector._peers.ToList();
            }
        }

        private class SubscriptionNode
        {
            private readonly bool _matchesAll;
            private readonly int _partIndex;
            private Dictionary<string, SubscriptionNode> _childrenNodes = new Dictionary<string, SubscriptionNode>();
            private List<Peer> _peers = new List<Peer>();
            private SubscriptionNode _sharpNode;
            private SubscriptionNode _starNode;

            public bool IsEmpty
            {
                get
                {
                    return _peers.Count == 0 &&
                           (_sharpNode == null || _sharpNode.IsEmpty) &&
                           (_starNode == null || _starNode.IsEmpty) &&
                           _childrenNodes.All(x => x.Value.IsEmpty);
                }
            }

            public SubscriptionNode(int partIndex, bool matchesAll = false)
            {
                _partIndex = partIndex;
                _matchesAll = matchesAll;
            }

            public void Accept(PeerCollector peerCollector, BindingKey bindingKey)
            {
                if (_partIndex == bindingKey.PartCount || _matchesAll)
                {
                    peerCollector.Offer(_peers);
                    return;
                }

                if (_sharpNode != null)
                    _sharpNode.Accept(peerCollector, bindingKey);

                if (_starNode != null)
                    _starNode.Accept(peerCollector, bindingKey);

                var part = bindingKey.GetPart(_partIndex);
                if (part == null)
                    return;

                SubscriptionNode childNode;
                if (_childrenNodes.TryGetValue(part, out childNode))
                    childNode.Accept(peerCollector, bindingKey);
            }


            public void Update(Peer peer, Subscription subscription, bool isAddOrUpdate)
            {
                if (IsLeaf(subscription) || IsMatchingAll(subscription))
                {
                    UpdateList(peer, isAddOrUpdate);
                    return;
                }

                var part = subscription.BindingKey.GetPart(_partIndex);

                if (part == "#" || part == null)
                {
                    var sharpNode = GetOrCreateSharpNode();
                    sharpNode.Update(peer, subscription, isAddOrUpdate);
                }
                else if (part == "*")
                {
                    var starNode = GetOrCreateStarNode();
                    starNode.Update(peer, subscription, isAddOrUpdate);
                }
                else
                {
                    var child = GetOrAddChildNode(part);
                    child.Update(peer, subscription, isAddOrUpdate);
                }
            }

            private bool IsMatchingAll(Subscription subscription)
            {
                return (_partIndex != 0 && subscription.IsMatchingAllMessages);
            }

            private bool IsLeaf(Subscription subscription)
            {
                return (_partIndex == subscription.BindingKey.PartCount && _partIndex != 0);
            }

            private SubscriptionNode CreateChildNode(bool matchesAll = false)
            {
                return new SubscriptionNode(_partIndex + 1, matchesAll);
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

            private void UpdateList(Peer peer, bool isAddOrUpdate)
            {
                var newPeers = _peers
                    .Where(i => i.Id != peer.Id)
                    .ToList();

                if (isAddOrUpdate)
                    newPeers.Add(peer);

                _peers = newPeers;
            }
        }
    }
}
