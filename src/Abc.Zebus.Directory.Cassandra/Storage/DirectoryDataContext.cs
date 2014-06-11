using Abc.Zebus.Directory.Cassandra.Cql;
using Cassandra.Data.EntityContext;

namespace Abc.Zebus.Directory.Cassandra.Storage
{
    public class DirectoryDataContext : CqlDataContext<ICassandraConfiguration>
    {
        public DirectoryDataContext(CassandraCqlSessionManager sessionManager, ICassandraConfiguration cassandraConfiguration)
            : base(sessionManager, cassandraConfiguration)
        {
            StoragePeers = AddTable<StoragePeer>();
        }

        public ContextTable<StoragePeer> StoragePeers { get; set; }
    }
}