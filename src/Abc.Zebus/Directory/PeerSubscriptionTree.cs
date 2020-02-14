using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Abc.Zebus.Routing;
using Abc.Zebus.Util.Extensions;
using JetBrains.Annotations;

namespace Abc.Zebus.Directory
{
    public class PeerSubscriptionTree
    {
        private readonly SubscriptionNode _rootNode = new SubscriptionNode(0);
        private List<Peer> _peersMatchingAllMessages = new List<Peer>();

        public bool IsEmpty => _rootNode.IsEmpty && _peersMatchingAllMessages.Count == 0;

        public void Add(Peer peer, BindingKey subscription)
            => UpdatePeerSubscription(peer, subscription, UpdateAction.Add);

        public void Remove(Peer peer, BindingKey subscription)
            => UpdatePeerSubscription(peer, subscription, UpdateAction.Remove);

        public IList<Peer> GetPeers(BindingKey routingKey)
        {
            var peerCollector = new PeerCollector(_peersMatchingAllMessages);

            if (routingKey.IsEmpty)
            {
                // The message is not routable or has no routing member.

                // If the tree contains any subscription with a binding key, it indicates a message definition
                // mismatch between the publisher and the subscriber. In this situation, it is safer to send
                // the message to the subscriber anyway.

                // => Always forward the message to all peers.

                _rootNode.AddAllPeers(peerCollector);
            }
            else
            {
                _rootNode.Accept(peerCollector, routingKey);
            }

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
            => UpdateList(ref _peersMatchingAllMessages, peer, action);

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

            [SuppressMessage("ReSharper", "ParameterTypeCanBeEnumerable.Local")]
            public void Offer(List<Peer> peers)
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
            private static readonly Action<SubscriptionNode, string> _removeNode = (x, part) => x.RemoveChildNode(part);
            private static readonly Action<SubscriptionNode, string> _removeSharpNode = (x, _) => x._sharpNode = null;
            private static readonly Action<SubscriptionNode, string> _removeStarNode = (x, _) => x._starNode = null;

            [CanBeNull]
            private ConcurrentDictionary<string, SubscriptionNode> _childNodes;

            [CanBeNull]
            private SubscriptionNode _sharpNode;

            [CanBeNull]
            private SubscriptionNode _starNode;

            private readonly int _nextPartIndex;
            private List<Peer> _peers = new List<Peer>();
            private int _peerCountIncludingChildren;

            public SubscriptionNode(int nextPartIndex)
            {
                _nextPartIndex = nextPartIndex;
            }

            public bool IsEmpty => _peerCountIncludingChildren == 0;

            public void AddAllPeers(PeerCollector peerCollector)
            {
                peerCollector.Offer(_peers);

                _sharpNode?.AddAllPeers(peerCollector);
                _starNode?.AddAllPeers(peerCollector);

                if (_childNodes == null)
                    return;

                foreach (var (_, childNode) in _childNodes)
                {
                    childNode.AddAllPeers(peerCollector);
                }
            }

            public void Accept(PeerCollector peerCollector, BindingKey routingKey)
            {
                if (IsLeaf(routingKey))
                {
                    peerCollector.Offer(_peers);
                    return;
                }

                _sharpNode?.AddAllPeers(peerCollector);
                _starNode?.Accept(peerCollector, routingKey);

                var nextPart = routingKey.GetPartToken(_nextPartIndex);
                if (nextPart == null || _childNodes == null)
                    return;

                if (_childNodes.TryGetValue(nextPart, out var childNode))
                    childNode.Accept(peerCollector, routingKey);
            }

            public int Update(Peer peer, BindingKey subscription, UpdateAction action)
            {
                if (IsLeaf(subscription))
                {
                    var update = UpdateList(peer, action);
                    _peerCountIncludingChildren += update;

                    return update;
                }

                var nextPart = subscription.GetPartToken(_nextPartIndex);

                if (subscription.IsSharp(_nextPartIndex) || nextPart == null)
                    return UpdateChildNode(GetOrCreateSharpNode(), peer, subscription, action, null, _removeSharpNode);

                if (subscription.IsStar(_nextPartIndex))
                    return UpdateChildNode(GetOrCreateStarNode(), peer, subscription, action, null, _removeStarNode);

                var childNode = GetOrAddChildNode(nextPart);
                return UpdateChildNode(childNode, peer, subscription, action, nextPart, _removeNode);
            }

            private int UpdateChildNode(SubscriptionNode childNode, Peer peer, BindingKey subscription, UpdateAction action, string childNodePart, Action<SubscriptionNode, string> remover)
            {
                var update = childNode.Update(peer, subscription, action);
                _peerCountIncludingChildren += update;

                if (childNode.IsEmpty)
                    remover(this, childNodePart);

                return update;
            }

            private bool IsLeaf(BindingKey bindingKey)
            {
                if (_nextPartIndex == 0)
                    return false;

                return _nextPartIndex == bindingKey.PartCount;
            }

            private SubscriptionNode GetOrAddChildNode(string part)
            {
                if (_childNodes == null)
                    _childNodes = new ConcurrentDictionary<string, SubscriptionNode>();

                return _childNodes.GetOrAdd(part, k => new SubscriptionNode(_nextPartIndex + 1));
            }

            private void RemoveChildNode(string part)
                => _childNodes?.TryRemove(part, out _);

            private SubscriptionNode GetOrCreateSharpNode()
                => _sharpNode ?? (_sharpNode = new SubscriptionNode(_nextPartIndex + 1));

            private SubscriptionNode GetOrCreateStarNode()
                => _starNode ?? (_starNode = new SubscriptionNode(_nextPartIndex + 1));

            private int UpdateList(Peer peer, UpdateAction action)
                => action == UpdateAction.Add
                    ? AddToList(peer)
                    : RemoveFromList(peer);

            private int AddToList(Peer peerToAdd)
            {
                var removed = false;
                var newPeers = new List<Peer>(_peers.Capacity);
                foreach (var peer in _peers)
                {
                    if (peer.Id == peerToAdd.Id)
                        removed = true;
                    else
                        newPeers.Add(peer);
                }

                newPeers.Add(peerToAdd);

                _peers = newPeers;

                return removed ? 0 : 1;
            }

            private int RemoveFromList(Peer peerToRemove)
            {
                var removed = false;
                var newPeers = new List<Peer>(_peers.Capacity);
                foreach (var peer in _peers)
                {
                    if (peer.Id == peerToRemove.Id)
                        removed = true;
                    else
                        newPeers.Add(peer);
                }

                _peers = newPeers;

                return removed ? -1 : 0;
            }
        }

        private enum UpdateAction
        {
            Add,
            Remove,
        }
    }
}
