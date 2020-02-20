using Abc.Zebus.Directory.Cassandra.Cql;
using Cassandra.Data.Linq;

namespace Abc.Zebus.Directory.Cassandra.Storage
{
    public class DirectoryDataContext : CqlDataContext<ICassandraConfiguration>
    {
        public DirectoryDataContext(CassandraCqlSessionManager sessionManager, ICassandraConfiguration cassandraConfiguration)
            : base(sessionManager, cassandraConfiguration)
        {
        }

        public Table<StorageSubscription> DynamicSubscriptions => new Table<StorageSubscription>(Session);
        public Table<StoragePeer> StoragePeers => new Table<StoragePeer>(Session);
    }
}
