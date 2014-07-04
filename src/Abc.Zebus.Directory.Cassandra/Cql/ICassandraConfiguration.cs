using System;

namespace Abc.Zebus.Directory.Cassandra.Cql
{
    public interface ICassandraConfiguration
    {
        /// <summary>
        /// The space separated list of hosts to connect to
        /// </summary>
        string Hosts { get; }

        /// <summary>
        /// The Keyspace where the Directory schema is stored
        /// </summary>
        string KeySpace { get; }

        /// <summary>
        /// Time after which a query will be retried on another node
        /// </summary>
        TimeSpan QueryTimeout { get; }

        /// <summary>
        /// Name of the local datacenter, this allows the CQL driver to favor local nodes
        /// (since the nodes provides in Hosts are only contact nodes and that the driver
        /// will discover the rest of the cluster)
        /// </summary>
        string LocalDataCenter { get; }
    }
}