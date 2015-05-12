using System;
using Cassandra.Mapping.Attributes;

namespace Abc.Zebus.Directory.Cassandra.Storage
{
    [Table("Peers")]
    public class StoragePeer
    {
        [PartitionKey]
        public bool UselessKey { get; set; }

        [ClusteringKey(0)]
        [Column("PeerId")]
        public string PeerId { get; set; }

        [Column("EndPoint")]
        public string EndPoint { get; set; }
            
        [Column("IsUp")]
        public bool IsUp { get; set; }

        [Column("IsResponding")]
        public bool IsResponding { get; set; }

        [Column("IsPersistent")]
        public bool IsPersistent { get; set; }

        [Column("TimestampUtc")]
        public DateTime TimestampUtc { get; set; }

        [Column("HasDebuggerAttached")]
        public bool HasDebuggerAttached { get; set; }

        [Column("StaticSubscriptions")]
        public byte[] StaticSubscriptionsBytes { get; set; }
            
        public StoragePeer()
        {
        }
    }
}