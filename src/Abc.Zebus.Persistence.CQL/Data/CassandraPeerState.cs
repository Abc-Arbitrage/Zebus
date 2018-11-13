using Abc.Zebus.Persistence.CQL.Storage;
using Cassandra.Mapping.Attributes;

namespace Abc.Zebus.Persistence.CQL.Data
{
    [Table("PeerState", CaseSensitive = true)]
    public class CassandraPeerState
    {
        public CassandraPeerState(PeerState peerState)
        {
            PeerId = peerState.PeerId.ToString();
            NonAckedMessageCount = peerState.NonAckedMessageCount;
            OldestNonAckedMessageTimestamp = peerState.OldestNonAckedMessageTimestampInTicks;
        }

        public CassandraPeerState()
        {
        }

        [PartitionKey]
        [Column("PeerId")]
        public string PeerId { get; set; }

        [Column("NonAckedMessageCount")]
        public int NonAckedMessageCount { get; set; }

        [Column("OldestNonAckedMessageTimestamp")]
        public long OldestNonAckedMessageTimestamp { get; set; }
    }
}
