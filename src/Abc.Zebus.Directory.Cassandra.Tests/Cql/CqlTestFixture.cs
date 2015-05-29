using System;
using System.Collections.Generic;
using System.Diagnostics;
using Abc.Zebus.Directory.Cassandra.Cql;
using Abc.Zebus.Util;
using Cassandra;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Directory.Cassandra.Tests.Cql
{
    // TODO: This entire namespace belongs in Zebus.Testing but it would require the creation of a Zebus.Shared, and we are not ready for that just yet
    public abstract class CqlTestFixture<TDataContext, TConfig>
        where TDataContext : CqlDataContext<TConfig>
        where TConfig : class, ICassandraConfiguration
    {
        private readonly string _keySpace = "UnitTesting_" + Guid.NewGuid().ToString().Substring(0, 8);

        private readonly CassandraCqlSessionManager _sessionManager = new CassandraCqlSessionManager();

        protected Cluster Cluster { get; private set; }

        public TDataContext DataContext { get; private set; }

        protected ISession Session { get; private set; }

        public Mock<TConfig> ConfigurationMock { get; private set; }

        protected abstract string Hosts { get; }

        protected abstract string LocalDataCenter { get; }

        [TestFixtureSetUp]
        public void CreateSchema()
        {
            Diagnostics.CassandraTraceSwitch.Level = TraceLevel.Info;
            Diagnostics.CassandraStackTraceIncluded = true;

            ConfigurationMock = CreateConfigurationMock();

            var strategyReplicationProperty = ReplicationStrategies.CreateNetworkTopologyStrategyReplicationProperty(new Dictionary<string, int> { { LocalDataCenter, 1 } });
            Cluster = _sessionManager.GetCluster(ConfigurationMock.Object);
            Cluster.ConnectAndCreateDefaultKeyspaceIfNotExists(strategyReplicationProperty).Dispose();

            Session = _sessionManager.GetSession(ConfigurationMock.Object);

            DataContext = CreateDataContext();
            DataContext.CreateTablesIfNotExist();
        }

        private TDataContext CreateDataContext()
        {
            // To enhance if needed
            return (TDataContext)Activator.CreateInstance(typeof(TDataContext), _sessionManager, ConfigurationMock.Object);
        }

        private Mock<TConfig> CreateConfigurationMock()
        {
            return new CassandraConfigurationMock<TConfig>(Hosts, _keySpace, LocalDataCenter, 5.Seconds());
        }

        [TestFixtureTearDown]
        public void DropSchema()
        {
            Session.Execute(new SimpleStatement(string.Format("drop keyspace \"{0}\";", _keySpace)));
            _sessionManager.Dispose();
        }

        [TearDown]
        public void TruncateAllColumnFamilies()
        {
            var tableNames = DataContext.GetTableNames();
            foreach (var name in tableNames)
                Session.Execute(new SimpleStatement(string.Format("truncate \"{0}\";", name)));
        }
    }
}