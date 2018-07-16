using Abc.Zebus.Persistence.CQL.Util;
using Cassandra.Data.Linq;

namespace Abc.Zebus.Persistence.CQL.Data
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