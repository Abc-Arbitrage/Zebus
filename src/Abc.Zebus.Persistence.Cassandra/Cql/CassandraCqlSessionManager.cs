using System;
using System.Collections.Concurrent;
using System.Linq;
using Cassandra;

namespace Abc.Zebus.Persistence.Cassandra.Cql
{
    public class CassandraCqlSessionManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, Cluster> _clusters = new ConcurrentDictionary<string, Cluster>();
        private readonly ConcurrentDictionary<Cluster, ISession> _sessions = new ConcurrentDictionary<Cluster, ISession>();

        private CassandraCqlSessionManager()
        {
        }

        public static CassandraCqlSessionManager Create()
        {
            return new CassandraCqlSessionManager();
        }

        private ISession GetOrCreateSession(Cluster cluster, string keySpace)
        {
            if (!_sessions.TryGetValue(cluster, out var session))
            {
                session = cluster.Connect(keySpace);
                _sessions.TryAdd(cluster, session);
            }

            return session;
        }

        private Cluster GetOrCreateCluster(string hosts, string defaultKeySpace, TimeSpan queryTimeout, string localDataCenter)
        {
            if (!_clusters.TryGetValue(hosts, out var cluster))
            {
                var contactPoints = hosts.Split(' ').ToArray();

                cluster = Cluster
                    .Builder()
                    .WithDefaultKeyspace(defaultKeySpace)
                    .WithQueryTimeout((int)queryTimeout.TotalMilliseconds)
                    .AddContactPoints(contactPoints)
                    .WithLoadBalancingPolicy(new DCAwareRoundRobinPolicy(localDataCenter))
                    .Build();

                _clusters.TryAdd(hosts, cluster);
            }

            return cluster;
        }

        public void Dispose()
        {
            foreach (var session in _sessions.Values)
                session.Dispose();

            foreach (var cluster in _clusters.Values)
                cluster.Dispose();
        }

        public ISession GetSession(ICassandraConfiguration configuration)
        {
            var cluster = GetOrCreateCluster(configuration.Hosts, configuration.KeySpace, configuration.QueryTimeout, configuration.LocalDataCenter);

            return GetOrCreateSession(cluster, configuration.KeySpace);
        }

        public Cluster GetCluster(ICassandraConfiguration configuration)
        {
            return GetOrCreateCluster(configuration.Hosts, configuration.KeySpace, configuration.QueryTimeout, configuration.LocalDataCenter);
        }
    }
}
