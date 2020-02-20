using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Abc.Zebus.Core
{
    internal class RoundRobinPeerSelector
    {
        private readonly ConcurrentDictionary<Type, int> _peerIndexes = new ConcurrentDictionary<Type, int>();

        public Peer? GetTargetPeer(ICommand command, IList<Peer> handlingPeers)
        {
            if (handlingPeers.Count == 1)
                return handlingPeers[0];

            if (handlingPeers.Count == 0)
                return null;

            var commandType = command.GetType();

            if (!_peerIndexes.TryGetValue(commandType, out var index))
                index = 0;

            if (index >= handlingPeers.Count)
                index = 0;

            var resolvedPeer = handlingPeers[index];

            _peerIndexes[commandType] = ++index;

            return resolvedPeer;
        }
    }
}
