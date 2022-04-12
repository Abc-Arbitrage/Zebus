using Abc.Zebus.Directory.Cassandra.Cql;
using Abc.Zebus.Directory.Cassandra.Data;
using Cassandra.Data.Linq;

namespace Abc.Zebus.Directory.Cassandra.Storage
{
    public class DirectoryDataContext : CqlDataContext<ICassandraConfiguration>
    {
        public DirectoryDataContext(CassandraCqlSessionManager sessionManager, ICassandraConfiguration cassandraConfiguration)
            : base(sessionManager, cassandraConfiguration)
        {
        }

        public Table<CassandraSubscription> DynamicSubscriptions => new(Session);
        public Table<CassandraPeer> Peers => new (Session);
    }
}
