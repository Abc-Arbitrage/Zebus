using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Abc.Zebus.Directory.Cassandra.Cql;
using Abc.Zebus.Directory.Cassandra.Storage;
using Abc.Zebus.Directory.Cassandra.Tests.Cql;
using Abc.Zebus.Routing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Testing.Measurements;
using NUnit.Framework;

namespace Abc.Zebus.Directory.Cassandra.Tests.Storage
{
    [TestFixture, Ignore("Performance tests")]
    public class CqlPeerRepositoryPerformanceTests : CqlTestFixture<DirectoryDataContext, ICassandraConfiguration>
    {
        protected override string Hosts
        {
            get { return "cassandra-test-host"; }
        }

        protected override string LocalDataCenter
        {
            get { return "Paris-CEN"; }
        }

        [Test]
        public void insert_30_peers_with_8000_subscriptions_each()
        {
            const int numberOfPeersToInsert = 30;
            var repo = new CqlPeerRepository(DataContext);
            var subscriptionForTypes = Get10MessageTypesWith800BindingKeysEach();
            
            for (var i = 0; i < numberOfPeersToInsert; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                repo.AddOrUpdatePeer(new PeerDescriptor(new PeerId("Abc.Peer." + i), "tcp://toto:123", true, true, true, DateTime.UtcNow));
                repo.AddDynamicSubscriptionsForTypes(new PeerId("Abc.Peer." + i), DateTime.UtcNow, subscriptionForTypes);
                Console.WriteLine("Batch: " + i + " Elapsed: " + stopwatch.Elapsed);
            }

            var sw = Stopwatch.StartNew();
            var peers = repo.GetPeers().ToList();
            Console.WriteLine("GetPeers() took " + sw.Elapsed);
            peers.Count.ShouldEqual(30);
            foreach (var peer in peers)
                peer.Subscriptions.Length.ShouldEqual(8000);
        }

        [Test]
        public void insert_1_peer_with_100_000_subscriptions()
        {
            var repo = new CqlPeerRepository(DataContext);
            var subscriptionForTypes = Get1MessageTypesWith100000BindingKeys();
            
            var stopwatch = Stopwatch.StartNew();
            repo.AddOrUpdatePeer(new PeerDescriptor(new PeerId("Abc.Peer.0"), "tcp://toto:123", true, true, true, DateTime.UtcNow));
            repo.AddDynamicSubscriptionsForTypes(new PeerId("Abc.Peer.0"), DateTime.UtcNow, subscriptionForTypes);
            Console.WriteLine("Elapsed: " + stopwatch.Elapsed);

            Measure.Execution(20, () => repo.GetPeers(loadDynamicSubscriptions: false).ToList());
            var peers = repo.GetPeers(loadDynamicSubscriptions: false).ToList();
            peers.ExpectedSingle().Subscriptions.Length.ShouldEqual(0);
        }

        private SubscriptionsForType[] Get1MessageTypesWith100000BindingKeys()
        {
            var bindingKeys = Enumerable.Range(1, 100000).Select(i => new BindingKey(i.ToString())).ToArray();
            return new[] { new SubscriptionsForType(new MessageTypeId("Abc.Namespace.MessageType"), bindingKeys) };
        }

        private static SubscriptionsForType[] Get10MessageTypesWith800BindingKeysEach()
        {
            var messageTypes = Enumerable.Range(1, 10).Select(i => "Abc.Namespace.MessageType" + i).ToList();
            var subscriptionForTypes = new List<SubscriptionsForType>();
            foreach (var messageType in messageTypes)
            {
                var bindingKeys = Enumerable.Range(1, 800).Select(i => new BindingKey(i.ToString())).ToArray();
                subscriptionForTypes.Add(new SubscriptionsForType(new MessageTypeId(messageType), bindingKeys));
            }
            return subscriptionForTypes.ToArray();
        }
    }
}