using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Directory;
using Abc.Zebus.Routing;
using Abc.Zebus.Testing.Measurements;
using Abc.Zebus.Tests.Messages;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Directory
{
    public partial class PeerSubscriptionTreeTests
    {
        [Test]
        [Explicit]
        [Category("ManualOnly")]
        [TestCase("a.e.f")]
        [TestCase("a.e")]
        [TestCase("a.b.c.d")]
        [TestCase("a")]
        public void MeasureDynamicSubscriptionsPerformance(string routingKey)
        {
            var subscriptions = GenerateSubscriptions().ToList();
            Console.WriteLine("{0} subscriptions", subscriptions.Count);
            Console.WriteLine();

            var subscriptionTree = new PeerSubscriptionTree();
            foreach (var peerSubscription in subscriptions)
            {
                subscriptionTree.Add(peerSubscription.Item1, peerSubscription.Item2.BindingKey);
            }

            var routingContent = RoutingContent.FromValues(routingKey.Split('.'));

            Console.WriteLine("{0} test -------------", subscriptionTree.GetType().Name);
            Console.WriteLine();
            Measure.Execution(x =>
            {
                x.Iteration = 10000;
                x.WarmUpIteration = 1000;
                x.Action = _ => subscriptionTree.GetPeers(routingContent);
            });
        }

        [Test, Explicit("Manual tests")]
        public void MeasureStaticSubscriptionsPerformance()
        {
            var peers = Enumerable.Range(0, 100).Select(x => new Peer(new PeerId("Abc.Testing" + x), "tcp://testing:" + x)).ToList();
            Console.WriteLine("{0} peers", peers.Count);
            Console.WriteLine();

            var subscriptionTree = new PeerSubscriptionTree();

            foreach (var peer in peers)
            {
                subscriptionTree.Add(peer, BindingKey.Empty);
            }

            Console.WriteLine("{0} test -------------", subscriptionTree.GetType().Name);
            Console.WriteLine();
            Measure.Execution(x =>
            {
                x.Iteration = 100000;
                x.WarmUpIteration = 1000;
                x.Action = _ => subscriptionTree.GetPeers(RoutingContent.Empty);
            });
        }

        private IEnumerable<Tuple<Peer, Subscription>> GenerateSubscriptions()
        {
            return from p in Enumerable.Range(0, 10)
                   let peer = new Peer(new PeerId(p.ToString()), "endpoint")
                   from l1 in "abcdef"
                   from l2 in "abcdef"
                   from l3 in "abcdef*"
                   let subscription = new Subscription(_messageTypeId, new BindingKey(l1.ToString(), l2.ToString(), l3.ToString()))
                   select new Tuple<Peer, Subscription>(peer, subscription);
        }
    }
}
