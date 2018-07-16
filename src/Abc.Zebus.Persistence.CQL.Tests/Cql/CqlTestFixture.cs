using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Abc.Zebus.Persistence.CQL.Testing;
using Abc.Zebus.Persistence.CQL.Util;
using Cassandra;
using Cassandra.Mapping;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.CQL.Tests.Cql
{
    // TODO: This entire namespace belongs in Zebus.Testing but it would require the creation of a Zebus.Shared, and we are not ready for that just yet
    public abstract class CqlTestFixture<TDataContext, TConfig>
        where TDataContext : CqlDataContext<TConfig>
        where TConfig : class, ICassandraConfiguration
    {
        protected string Hosts => "cassandra-test-host";
        protected string LocalDataCenter => "Paris-ABC";

        private readonly string _keySpace = "UnitTesting_" + Guid.NewGuid().ToString().Substring(0, 8);

        private readonly CassandraCqlSessionManager _sessionManager = CassandraCqlSessionManager.Create();

        protected Cluster Cluster { get; private set; }

        public TDataContext DataContext { get; private set; }

        protected ISession Session { get; private set; }

        public Mock<TConfig> ConfigurationMock { get; private set; }
        
        [OneTimeSetUp]
        public virtual void CreateSchema()
        {
            Diagnostics.CassandraTraceSwitch.Level = TraceLevel.Info;
            Diagnostics.CassandraStackTraceIncluded = true;

            ClearDriverInternalCache();
            ConfigurationMock = CreateConfigurationMock();

            var strategyReplicationProperty = ReplicationStrategies.CreateNetworkTopologyStrategyReplicationProperty(new Dictionary<string, int> { { LocalDataCenter, 1 } });
            Cluster = _sessionManager.GetCluster(ConfigurationMock.Object);
            Cluster.ConnectAndCreateDefaultKeyspaceIfNotExists(strategyReplicationProperty).Dispose();

            Session = _sessionManager.GetSession(ConfigurationMock.Object);

            DataContext = CreateDataContext();
            DataContext.CreateTablesIfNotExist();
        }

        private static void ClearDriverInternalCache()
        {
            // Remove this when https://datastax-oss.atlassian.net/browse/CSHARP-298 is fixed
            var clearMethod = typeof(MappingConfiguration).GetMethod("Clear", BindingFlags.Instance | BindingFlags.NonPublic);
            clearMethod.Invoke(MappingConfiguration.Global, new object[0]);
        }

        private TDataContext CreateDataContext()
        {
            // To enhance if needed
            return (TDataContext)Activator.CreateInstance(typeof(TDataContext), _sessionManager, ConfigurationMock.Object);
        }

        private Mock<TConfig> CreateConfigurationMock()
        {
            return new CassandraConfigurationMock<TConfig>(Hosts, _keySpace, LocalDataCenter);
        }

        [OneTimeTearDown]
        public void DropSchema()
        {
            Session.Execute(new SimpleStatement($"drop keyspace \"{_keySpace}\";"));
            _sessionManager.Dispose();
        }

        [TearDown]
        public void TruncateAllColumnFamilies()
        {
            var tableNames = DataContext.GetTableNames();
            foreach (var name in tableNames)
                Session.Execute(new SimpleStatement($"truncate \"{name}\";"));
        }
    }
}
