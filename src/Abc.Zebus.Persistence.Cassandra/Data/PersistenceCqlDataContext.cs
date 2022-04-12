using Abc.Zebus.Persistence.Cassandra.Cql;
using Cassandra.Data.Linq;

namespace Abc.Zebus.Persistence.Cassandra.Data
{
    public class PersistenceCqlDataContext : CqlDataContext<ICqlPersistenceConfiguration>
    {
        public PersistenceCqlDataContext(CassandraCqlSessionManager sessionManager, ICqlPersistenceConfiguration cassandraConfiguration) : base(sessionManager, cassandraConfiguration)
        {
        }

        public Table<PersistentMessage> PersistentMessages => new Table<PersistentMessage>(Session);
        public Table<CassandraPeerState> PeerStates => new Table<CassandraPeerState>(Session);
    }
}
