using Cassandra;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Abc.Zebus.Persistence.Cassandra.Cql
{
    public class CassandraCqlSessionManager : IDisposable
    {
        private static readonly ILogger _logger = ZebusLogManager.GetLogger(typeof(CassandraCqlSessionManager));

        private readonly ConcurrentDictionary<string, Cluster> _clusters = new();
        private readonly ConcurrentDictionary<Cluster, ISession> _sessions = new();

        private CassandraCqlSessionManager()
        {
        }

        public static CassandraCqlSessionManager Create()
        {
            return new CassandraCqlSessionManager();
        }

        private ISession GetOrCreateSession(Cluster cluster, string keySpace)
        {
            if (_sessions.TryGetValue(cluster, out var session))
                return session;

            session = cluster.Connect(keySpace);
            _sessions.TryAdd(cluster, session);

            return session;
        }

        private Cluster GetOrCreateCluster(ICassandraConfiguration configuration)
        {
            if (_clusters.TryGetValue(configuration.Hosts, out var cluster))
                return cluster;

            var contactPoints = configuration.Hosts.Split(' ');

            var clusterBuilder = Cluster
                                 .Builder()
                                 .WithDefaultKeyspace(configuration.KeySpace)
                                 .WithQueryTimeout((int)configuration.QueryTimeout.TotalMilliseconds)
                                 .AddContactPoints(contactPoints)
                                 .WithLoadBalancingPolicy(new DCAwareRoundRobinPolicy(configuration.LocalDataCenter));

            if (configuration.UseSsl)
                clusterBuilder = clusterBuilder.WithSSL(new SSLOptions(SslProtocols.None, false, ValidateServerCertificate));

            cluster = clusterBuilder.Build();

            _clusters.TryAdd(configuration.Hosts, cluster);

            return cluster;

            bool ValidateServerCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
            {
                if (sslPolicyErrors == SslPolicyErrors.None)
                    return true;

                _logger.Log(LogLevel.Error, "Failed to validate certificate: {SslPolicyErrors}", sslPolicyErrors);
                return false;
            }
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
            var cluster = GetOrCreateCluster(configuration);

            return GetOrCreateSession(cluster, configuration.KeySpace);
        }

        public Cluster GetCluster(ICassandraConfiguration configuration)
        {
            return GetOrCreateCluster(configuration);
        }
    }
}
