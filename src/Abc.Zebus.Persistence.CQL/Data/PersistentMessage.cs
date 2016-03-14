using Cassandra.Mapping.Attributes;

namespace Abc.Zebus.Persistence.CQL.Data
{
    [Table("PersistentMessage", CaseSensitive = true)]
    public class PersistentMessage
    {
        [PartitionKey(0)]
        [Column("PeerId")]
        public string PeerId { get; set; }

        [PartitionKey(1)]
        [Column("BucketId")]
        public long BucketId { get; set; }

        [ClusteringKey]
        [Column("UniqueTimestampInTicks")]
        public long UniqueTimestampInTicks { get; set; }

        [Column("IsAcked")]
        public bool IsAcked { get; set; }

        [Column("TransportMessage")]
        public byte[] TransportMessage { get; set; }
    }
}