﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.Cassandra.Data;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Persistence.Util;
using Cassandra;
using Cassandra.Data.Linq;
using Microsoft.Extensions.Logging;

namespace Abc.Zebus.Persistence.Cassandra.Cql
{
    public class PeerStateRepository
    {
        private static readonly ILogger _log = ZebusLogManager.GetLogger(typeof(PeerStateRepository));

        private readonly PersistenceCqlDataContext _dataContext;
        private readonly ConcurrentDictionary<PeerId, PeerState> _statesByPeerId = new ConcurrentDictionary<PeerId, PeerState>();

        public PeerStateRepository(PersistenceCqlDataContext dataContext)
        {
            _dataContext = dataContext;
        }

        public Func<DateTime> DateTimeSource { get; set; } = () => DateTime.UtcNow;

        public void Initialize()
        {
            _log.LogInformation("Initializing PeerStateRepository");

            foreach (var cassandraPeerState in _dataContext.PeerStates.Execute())
            {
                var peerState = new PeerState(new PeerId(cassandraPeerState.PeerId), cassandraPeerState.NonAckedMessageCount, cassandraPeerState.OldestNonAckedMessageTimestamp);
                _statesByPeerId[peerState.PeerId] = peerState;
            }

            _log.LogInformation($"PeerStateRepository initialized with {_statesByPeerId.Count} states.");
        }

        public PeerState? GetPeerStateFor(PeerId peerId)
        {
            return _statesByPeerId.GetValueOrDefault(peerId);
        }

        public Task UpdateNonAckMessageCount(PeerId peerId, int delta)
        {
            var peerState = _statesByPeerId.AddOrUpdate(peerId,
                                                          p =>
                                                          {
                                                              _log.LogInformation($"Created new state for peer {p}");
                                                              return new PeerState(p, delta, MinimumOldestNonAckedMessageTimestamp);
                                                          },
                                                          (id, state) => state.WithNonAckedMessageCountDelta(delta));

            return UpdatePeerState(peerState);
        }

        public Task RemovePeer(PeerId peerId)
        {
            _log.LogInformation($"Purge queue for peer {peerId} requested");

            if (!_statesByPeerId.TryRemove(peerId, out var state))
            {
                _log.LogInformation($"Peer to purge not found ({peerId})");
                return Task.CompletedTask;
            }

            state.MarkAsRemoved();

            var removeTask = Task.WhenAll(DeletePeerState(peerId), RemovePersistentMessages(peerId));
            removeTask.ContinueWith(t => _log.LogInformation($"Queue for peer {peerId} purged"));

            return removeTask;
        }

        public IEnumerable<PeerState> GetAllKnownPeers()
        {
            return _statesByPeerId.Values;
        }

        public Task UpdateNewOldestMessageTimestamp(PeerState peer, long newOldestMessageTimestamp)
        {
            var updatedPeer = _statesByPeerId.AddOrUpdate(peer.PeerId,
                                                          id => new PeerState(id, 0, newOldestMessageTimestamp),
                                                          (id, state) => state.WithOldestNonAckedMessageTimestampInTicks(newOldestMessageTimestamp));

            return UpdatePeerState(updatedPeer);
        }

        private Task<RowSet> DeletePeerState(PeerId peerId)
        {
            return _dataContext.PeerStates.Where(x => x.PeerId == peerId.ToString()).Delete().ExecuteAsync();
        }

        private Task RemovePersistentMessages(PeerId peerId)
        {
            var allPossibleBuckets = BucketIdHelper.GetBucketsCollection(MinimumOldestNonAckedMessageTimestamp, DateTimeSource.Invoke()).ToArray();

            return _dataContext.PersistentMessages
                               .Where(x => x.PeerId == peerId.ToString() && allPossibleBuckets.Contains(x.BucketId))
                               .Delete()
                               .ExecuteAsync();
        }

        private Task<RowSet> UpdatePeerState(PeerState p)
        {
            return _dataContext.PeerStates.Insert(new CassandraPeerState(p)).ExecuteAsync();
        }

        private long MinimumOldestNonAckedMessageTimestamp => DateTimeSource.Invoke().Ticks - PeerState.MessagesTimeToLive.Ticks;
    }
}
