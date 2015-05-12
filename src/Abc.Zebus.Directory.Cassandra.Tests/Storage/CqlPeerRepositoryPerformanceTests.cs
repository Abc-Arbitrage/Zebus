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
using Cassandra;
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
            Diagnostics.CassandraTraceSwitch.Level = TraceLevel.Off;
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
            Diagnostics.CassandraTraceSwitch.Level = TraceLevel.Info;
            var repo = new CqlPeerRepository(DataContext);
            var subscriptionForTypes = Get1MessageTypesWith100000BindingKeys();
            

            var stopwatch = Stopwatch.StartNew();
            repo.AddOrUpdatePeer(new PeerDescriptor(new PeerId("Abc.Peer.0"), "tcp://toto:123", true, true, true, DateTime.UtcNow));
            repo.AddDynamicSubscriptionsForTypes(new PeerId("Abc.Peer.0"), DateTime.UtcNow, subscriptionForTypes);
            Console.WriteLine("Elapsed: " + stopwatch.Elapsed);

            Measure.Execution(100, () => repo.GetPeers(loadDynamicSubscriptions: false).ToList());
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

        // UselessKey
        //        100 iterations in 953 ms (105,0 iterations/sec)
        //        Latencies :
        //        Min :               6 068µs
        //        Avg :               9 526µs
        //        Median :            6 785µs
        //        95 percentile :     11 572µs
        //        99 percentile :     50 912µs
        //        Max :             156 487µs (Iteration #0)
        //        G0 : 18
        //        G1 : 0
        //        G2 : 0
        //          Expected: 8000
        //          But was:  0

        // Clean
        //        100 iterations in 1 242 ms (80,5 iterations/sec)
        //        Latencies :
        //        Min :               9 659µs
        //        Avg :              12 416µs
        //        Median :           10 291µs
        //        95 percentile :     12 751µs
        //        99 percentile :     65 848µs
        //        Max :             109 858µs (Iteration #0)
        //        G0 : 17
        //        G1 : 0
        //        G2 : 0
        //          Expected: 8000
        //          But was:  0
    }
}