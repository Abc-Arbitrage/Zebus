using Cassandra.Mapping.Attributes;

namespace Abc.Zebus.Directory.Cassandra.Storage
{
    [Table("DynamicSubscriptions", CaseSensitive = true)]
    public class StorageSubscription
    {
        [PartitionKey]
        public bool UselessKey { get; set; }

        [ClusteringKey(0)]
        [Column("PeerId")]
        public string PeerId { get; set; } = default!;

        [ClusteringKey(1)]
        [Column("MessageTypeId")]
        public string MessageTypeId { get; set; } = default!;

        [Column("SubscriptionBindings")]
        public byte[] SubscriptionBindings { get; set; } = default!;
    }
}
