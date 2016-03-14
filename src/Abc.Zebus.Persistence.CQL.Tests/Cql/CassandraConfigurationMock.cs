using System;
using Abc.Zebus.Persistence.CQL.Util;
using Abc.Zebus.Persistence.Util;
using Moq;

namespace Abc.Zebus.Persistence.CQL.Tests.Cql
{
    // TODO: This entire namespace belongs in Zebus.Testing but it would require the creation of a Zebus.Shared, and we are not ready for that just yet
    public class CassandraConfigurationMock<TConfig> : Mock<TConfig> where TConfig : class, ICassandraConfiguration
    {
        public CassandraConfigurationMock(string host, string keySpace, string localDataCenter, TimeSpan? queryTimeout = null)
        {
            As<ICassandraConfiguration>().SetupGet(config => config.Hosts).Returns(host);
            As<ICassandraConfiguration>().SetupGet(config => config.KeySpace).Returns(keySpace);
            As<ICassandraConfiguration>().SetupGet(config => config.QueryTimeout).Returns(queryTimeout ?? 10.Seconds());
            As<ICassandraConfiguration>().SetupGet(config => config.LocalDataCenter).Returns(localDataCenter);
        }
    }
}