using System;

namespace Abc.Zebus.Directory.Cassandra.Cql
{
    public interface ICassandraConfiguration
    {
        string Hosts { get; }

        string KeySpace { get; }

        TimeSpan QueryTimeout { get; }

        string LocalDataCenter { get; }
    }
}