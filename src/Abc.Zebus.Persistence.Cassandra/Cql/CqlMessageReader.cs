using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Persistence.Cassandra.Data;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Persistence.Storage;
using Cassandra;
using Cassandra.Data.Linq;
using Microsoft.Extensions.Logging;

namespace Abc.Zebus.Persistence.Cassandra.Cql
{
    public class CqlMessageReader : IMessageReader
    {
        private static readonly ILogger _log = ZebusLogManager.GetLogger(typeof(CqlMessageReader));

        private readonly PersistenceCqlDataContext _dataContext;
        private readonly PeerState _peerState;
        private readonly PreparedStatement _preparedStatement;

        public CqlMessageReader(PersistenceCqlDataContext dataContext, PeerState peerState)
        {
            _dataContext = dataContext;
            _peerState = peerState;
            _preparedStatement = _dataContext.Session.Prepare(_dataContext.PersistentMessages
                                                                          .Where(x => x.PeerId == _peerState.PeerId.ToString()
                                                                                      && x.BucketId == 0
                                                                                      && x.UniqueTimestampInTicks >= 0)
                                                                          .Select(x => new { x.IsAcked, x.TransportMessage })
                                                                          .ToString());
        }

        public IEnumerable<byte[]> GetUnackedMessages()
        {
            var oldestNonAckedMessageTimestampInTicks = _peerState.OldestNonAckedMessageTimestampInTicks;
            _log.LogInformation($"Reading messages for peer {_peerState.PeerId} from {oldestNonAckedMessageTimestampInTicks} ({new DateTime(oldestNonAckedMessageTimestampInTicks).ToLongTimeString()})");

            var nonAckedMessagesInBuckets = BucketIdHelper.GetBucketsCollection(oldestNonAckedMessageTimestampInTicks)
                                                          .Select(b => GetNonAckedMessagesInBucket(oldestNonAckedMessageTimestampInTicks, b));

            var nonAckedMessageRead = 0;
            foreach (var nonAckedMessagesInBucket in nonAckedMessagesInBuckets)
            {
                foreach (var nonAckedMessage in nonAckedMessagesInBucket)
                {
                    nonAckedMessageRead++;
                    yield return nonAckedMessage;
                }
            }

            _log.LogInformation($"{nonAckedMessageRead} non acked messages replayed for peer {_peerState.PeerId}");
        }

        private IEnumerable<byte[]> GetNonAckedMessagesInBucket(long oldestNonAckedMessageTimestampInTicks, long bucketId)
        {
            return _dataContext.Session
                               .Execute(_preparedStatement.Bind(_peerState.PeerId.ToString(), bucketId, oldestNonAckedMessageTimestampInTicks).SetPageSize(10 * 1000))
                               .Where(x => !x.GetValue<bool>("IsAcked"))
                               .Select(row => row.GetValue<byte[]>("TransportMessage"));
        }

        public void Dispose()
        {
            _log.LogInformation($"Reader for peer {_peerState.PeerId} disposed");
        }
    }
}
