using Cassandra;
using Cassandra.Data.EntityContext;
using Cassandra.Data.Linq;

namespace Abc.Zebus.Directory.Cassandra.Cql
{
    public abstract class CqlDataContext<TConfig> : Context where TConfig : ICassandraConfiguration
    {
        public CqlDataContext(CassandraCqlSessionManager sessionManager, TConfig cassandraConfiguration)
            : this(CreateSession(sessionManager, cassandraConfiguration))
        {
        }

        private CqlDataContext(ISession session)
            : base(session)
        {
            Session = session;
        }

        public ISession Session { get; private set; }

        protected static ISession CreateSession(CassandraCqlSessionManager sessionManager, ICassandraConfiguration cassandraConfiguration)
        {
            return sessionManager.GetSession(cassandraConfiguration);
        }
    }
}