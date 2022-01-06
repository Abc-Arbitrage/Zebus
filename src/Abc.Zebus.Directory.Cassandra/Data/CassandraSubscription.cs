using Cassandra.Mapping.Attributes;

namespace Abc.Zebus.Directory.Cassandra.Data
{
    [Table("DynamicSubscriptions_2", CaseSensitive = true)]
    public class CassandraSubscription
    {
        [PartitionKey, Column("PeerId")]
        public string PeerId { get; set; } = default!;

        [ClusteringKey(0), Column("MessageTypeId")]
        public string MessageTypeId { get; set; } = default!;

        [Column("SubscriptionBindings")]
        public byte[] SubscriptionBindings { get; set; } = default!;
    }
}
