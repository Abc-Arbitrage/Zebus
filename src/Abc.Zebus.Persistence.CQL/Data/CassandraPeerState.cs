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

        [PartitionKey(0)]
        [Column("PeerId")]
        public string PeerId { get; set; }

        [ClusteringKey]
        [Column("NonAckedMessageCount")]
        public int NonAckedMessageCount { get; set; }

        [ClusteringKey]
        [Column("OldestNonAckedMessageTimestamp")]
        public long OldestNonAckedMessageTimestamp { get; set; }
    }
}