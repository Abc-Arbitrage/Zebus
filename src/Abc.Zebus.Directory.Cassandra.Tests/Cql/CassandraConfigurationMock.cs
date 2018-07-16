using System;
using Abc.Zebus.Directory.Cassandra.Cql;
using Moq;

namespace Abc.Zebus.Directory.Cassandra.Tests.Cql
{
    // TODO: This entire namespace belongs in Zebus.Testing but it would require the creation of a Zebus.Shared, and we are not ready for that just yet
    public class CassandraConfigurationMock<TConfig> : Mock<TConfig> where TConfig : class, ICassandraConfiguration
    {
        public CassandraConfigurationMock(string host, string keySpace, string localDataCenter, TimeSpan queryTimeout)
        {
            As<ICassandraConfiguration>().SetupGet(config => config.Hosts).Returns(host);
            As<ICassandraConfiguration>().SetupGet(config => config.KeySpace).Returns(keySpace);
            As<ICassandraConfiguration>().SetupGet(config => config.QueryTimeout).Returns(queryTimeout);
            As<ICassandraConfiguration>().SetupGet(config => config.LocalDataCenter).Returns(localDataCenter);
        }
    }
}