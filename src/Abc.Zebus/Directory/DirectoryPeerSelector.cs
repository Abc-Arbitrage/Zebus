using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Util;
using Abc.Zebus.Util.Extensions;

namespace Abc.Zebus.Directory
{
    internal class DirectoryPeerSelector
    {
        private static readonly TimeSpan _faultedDirectoryRetryDelay = 30.Seconds();

        private readonly Dictionary<string, EndPointState> _endPointStates = new(StringComparer.OrdinalIgnoreCase);
        private readonly Random _random = new();
        private readonly object _lock = new();
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
            var peerStates = GetPeerStates(endPoints);

            return peerStates.OrderBy(x => x.IsFaulty).ThenBy(x => x.Priority).Select(x => x.Peer);
        }

        private List<PeerState> GetPeerStates(string[] endPoints)
        {
            var now = SystemDateTime.UtcNow;
            var peerStates = new List<PeerState>();
            var isDirectoryPickedRandomly = _configuration.IsDirectoryPickedRandomly;

            lock (_lock)
            {
                foreach (var(endPoint, index) in endPoints.Select((x, index) => (x, index)))
                {
                    var endPointState = _endPointStates.GetValueOrAdd(endPoint, () => new EndPointState());
                    var isFaulty = now < endPointState.ErrorTimestampUtc + _faultedDirectoryRetryDelay;
                    var priority = isDirectoryPickedRandomly ? _random.Next() : index;

                    peerStates.Add(new PeerState(index, endPoint, isFaulty, priority));
                }

                return peerStates;
            }
        }

        public void SetFaultedDirectory(Peer peer)
        {
            lock (_lock)
            {
                var endPointState = _endPointStates.GetValueOrAdd(peer.EndPoint, () => new EndPointState());
                endPointState.ErrorTimestampUtc = SystemDateTime.UtcNow;
            }
        }

        private class EndPointState
        {
            public DateTime ErrorTimestampUtc { get; set; }
        }

        private class PeerState
        {
            public PeerState(int index, string endPoint, bool isFaulty, int priority)
            {
                Peer = new Peer(new PeerId($"Abc.Zebus.DirectoryService.{index}"), endPoint);
                IsFaulty = isFaulty;
                Priority = priority;
            }

            public Peer Peer { get; }
            public bool IsFaulty { get; }
            public int Priority { get; }
        }
    }
}
