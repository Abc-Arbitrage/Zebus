using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Persistence.CQL.Data;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Persistence.Storage;
using Abc.Zebus.Transport;
using Cassandra;
using Cassandra.Data.Linq;
using log4net;

namespace Abc.Zebus.Persistence.CQL.Storage
{
    public class CqlMessageReader : IMessageReader
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(CqlMessageReader));

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

            DeserializeTransportMessage = TransportMessageDeserializer.Deserialize;
        }

        public Func<byte[], TransportMessage> DeserializeTransportMessage { get; set; } 

        public IEnumerable<TransportMessage> GetUnackedMessages()
        {
            var oldestNonAckedMessageTimestampInTicks = _peerState.OldestNonAckedMessageTimestampInTicks;
            _log.Info($"Reading messages for peer {_peerState.PeerId} from {oldestNonAckedMessageTimestampInTicks} ({new DateTime(oldestNonAckedMessageTimestampInTicks).ToLongTimeString()})");

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

            _log.Info($"{nonAckedMessageRead} non acked messages replayed for peer {_peerState.PeerId}");
        }

        private IEnumerable<TransportMessage> GetNonAckedMessagesInBucket(long oldestNonAckedMessageTimestampInTicks, long bucketId)
        {
            return _dataContext.Session.Execute(_preparedStatement.Bind(_peerState.PeerId.ToString(), bucketId, oldestNonAckedMessageTimestampInTicks).SetPageSize(10 * 1000))
                               .Where(x => !x.GetValue<bool>("IsAcked"))
                               .Select(CreatePersistentMessageFromRow);
        }

        private TransportMessage CreatePersistentMessageFromRow(Row row)
        {
            return DeserializeTransportMessage(row.GetValue<byte[]>("TransportMessage"));
        }

        public void Dispose()
        {
            _log.Info($"Reader for peer {_peerState.PeerId} disposed");
        }
    }
}