using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Util.Extensions;

namespace Abc.Zebus.Directory
{
    internal class DirectoryPeerSelector
    {
        private readonly IBusConfiguration _configuration;
        private string[]? _cachedEndPoints;

        public DirectoryPeerSelector(IBusConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IEnumerable<Peer> GetPeers()
        {
            _cachedEndPoints = _configuration.DirectoryServiceEndPoints;

            return GetPeersImpl(_cachedEndPoints);
        }

        public IEnumerable<Peer> GetPeersFromCache()
        {
            var endPoints = _cachedEndPoints ?? _configuration.DirectoryServiceEndPoints;

            return GetPeersImpl(endPoints);
        }

        private IEnumerable<Peer> GetPeersImpl(string[] endPoints)
        {
            var peers = endPoints.Select(CreateDirectoryPeer);

            return _configuration.IsDirectoryPickedRandomly ? peers.Shuffle() : peers;
        }

        private static Peer CreateDirectoryPeer(string endPoint, int index)
        {
            var peerId = new PeerId("Abc.Zebus.DirectoryService." + index);
            return new Peer(peerId, endPoint);
        }
    }
}
