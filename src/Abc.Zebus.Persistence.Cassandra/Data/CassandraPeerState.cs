using Abc.Zebus.Persistence.Cassandra.Cql;
using Cassandra.Mapping.Attributes;

namespace Abc.Zebus.Persistence.Cassandra.Data
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
        public string PeerId { get; set; } = default!;

        [Column("NonAckedMessageCount")]
        public int NonAckedMessageCount { get; set; }

        [Column("OldestNonAckedMessageTimestamp")]
        public long OldestNonAckedMessageTimestamp { get; set; }
    }
}
