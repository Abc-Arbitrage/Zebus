using Cassandra;
using Cassandra.Data.Linq;

namespace Abc.Zebus.Directory.Cassandra.Cql
{
    public abstract class CqlDataContext<TConfig> : Context where TConfig : ICassandraConfiguration
    {
        public CqlDataContext(CassandraCqlSessionManager sessionManager, TConfig cassandraConfiguration)
            : this(CreateSession(sessionManager, cassandraConfiguration))
        {
        }

        private CqlDataContext(Session session)
            : base(session)
        {
            Session = session;
        }

        public Session Session { get; private set; }

        protected static Session CreateSession(CassandraCqlSessionManager sessionManager, ICassandraConfiguration cassandraConfiguration)
        {
            return sessionManager.GetSession(cassandraConfiguration);
        }
    }
}