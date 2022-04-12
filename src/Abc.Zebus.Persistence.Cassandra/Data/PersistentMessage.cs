using System;
using Cassandra.Mapping.Attributes;

namespace Abc.Zebus.Persistence.Cassandra.Data
{
    [Table("PersistentMessage", CaseSensitive = true)]
    public class PersistentMessage
    {
        [PartitionKey(0)]
        [Column("PeerId")]
        public string PeerId { get; set; } = default!;

        [PartitionKey(1)]
        [Column("BucketId")]
        public long BucketId { get; set; }

        [ClusteringKey(0)]
        [Column("UniqueTimestampInTicks")]
        public long UniqueTimestampInTicks { get; set; }

        [ClusteringKey(1)]
        [Column("MessageId")]
        public Guid MessageId { get; set; }

        [Column("IsAcked")]
        public bool IsAcked { get; set; }

        [Column("TransportMessage")]
        public byte[] TransportMessage { get; set; } = default!;
    }
}
