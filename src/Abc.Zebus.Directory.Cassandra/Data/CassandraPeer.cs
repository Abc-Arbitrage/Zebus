using System;
using Cassandra.Mapping.Attributes;

namespace Abc.Zebus.Directory.Cassandra.Data
{
    [Table("Peers_2", CaseSensitive = true)]
    public class CassandraPeer
    {
        [PartitionKey, Column("PeerId")]
        public string PeerId { get; set; } = default!;

        [Column("EndPoint")]
        public string EndPoint { get; set; } = default!;

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
        public byte[] StaticSubscriptionsBytes { get; set; } = default!;

    }
}
