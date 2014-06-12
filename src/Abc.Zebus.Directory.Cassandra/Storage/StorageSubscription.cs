using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Cassandra.Data.Linq;

namespace Abc.Zebus.Directory.Cassandra.Storage
{
    [Table("DynamicSubscriptions")]
    public class StorageSubscription
    {
        [PartitionKey]
        public string PeerId { get; set; }

        [ClusteringKey(0)]
        [Column("SubscriptionIdentifier")]
        public Guid SubscriptionIdentifier
        {
            get { return HashToGuid((BindingKeyParts ?? new Dictionary<int, string>()).OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).Concat(new[] { MessageTypeId })); }
            set { }
        }

        [Column("MessageTypeId")]
        public string MessageTypeId { get; set; }

        // Favoring a map<int, text> over list<text> allows use to limit tombstone generation if a peer always recreates massively the same subscriptions
        [Column("BindingKeyParts")]
        public Dictionary<int, string> BindingKeyParts { get; set; }

        public static Guid HashToGuid(IEnumerable<string> input)
        {
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.Unicode.GetBytes(String.Concat(input)));
                return new Guid(hash);
            }
        }
    }
}