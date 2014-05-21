using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Directory;
using Abc.Zebus.Routing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Testing.Measurements;
using Abc.Zebus.Tests.Messages;
using NUnit.Framework;

namespace Abc.Zebus.Tests
{
    [TestFixture]
    public class PeerSubscriptionTreeTests
    {
        private readonly MessageTypeId _messageTypeId = new MessageTypeId(typeof(FakeCommand));

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

        [Ignore]
        [TestCase("a.e.f")]
        [TestCase("a.e")]
        [TestCase("a.b.c.d")]
        [TestCase("a")]
        public void Performance_test(string routingKey)
        {
            var subscriptions = GenerateSubscriptions().ToList();
            Console.WriteLine("{0} subscriptions", subscriptions.Count);
            Console.WriteLine();
            var subscriptionList = new PeerSubscriptionList();
            foreach (var peerSubscription in subscriptions)
            {
                subscriptionList.Add(peerSubscription.Item1, peerSubscription.Item2);
            }

            var subscriptionTree = new PeerSubscriptionTree();
            foreach (var peerSubscription in subscriptions)
            {
                subscriptionTree.Add(peerSubscription.Item1, peerSubscription.Item2);
            }

            var bindingKey = BindingKey.Split(routingKey);

            const int iterationCount = 10000;
            const int warmUpIterationCount = 100;
            
            Console.WriteLine("{0} test -------------", subscriptionList.GetType().Name);
            Console.WriteLine();
            Measure.Execution(x =>
            {
                x.Iteration = iterationCount;
                x.WarmUpIteration = warmUpIterationCount;
            }, _ => subscriptionList.GetPeers(bindingKey));
            Console.WriteLine();

            Console.WriteLine("{0} test -------------", subscriptionTree.GetType().Name);
            Console.WriteLine();
            Measure.Execution(x =>
            {
                x.Iteration = iterationCount;
                x.WarmUpIteration = warmUpIterationCount;
            }, _ => subscriptionTree.GetPeers(bindingKey));
        }

        private Subscription CreateSubscription(string bindingKey)
        {
            return new Subscription(new MessageTypeId(typeof(FakeCommand)), BindingKey.Split(bindingKey));
        }

        [TestCase("whatever")]
        [TestCase("*")]
        [TestCase("#")]
        public void single_star_should_always_match(string routingKey)
        {
            // Arrange
            var peerSubscriptionTree = new PeerSubscriptionTree();
            var peer = new Peer(new PeerId("jesuistonpeer"), "endpoint");
            var subscription = CreateSubscription("*");
            peerSubscriptionTree.Add(peer, subscription);

            // Act
            var matchingPeers = peerSubscriptionTree.GetPeers(BindingKey.Split(routingKey));

            // Assert
            matchingPeers.Single().ShouldEqual(peer);
        }

        [TestCase("whatever")]
        [TestCase("*")]
        [TestCase("#")]
        public void empty_bindingkey_should_always_match(string routingKey)
        {
            // Arrange
            var peerSubscriptionTree = new PeerSubscriptionTree();
            var peer = new Peer(new PeerId("jesuistonpeer"), "endpoint");
            var subscription = new Subscription(new MessageTypeId(typeof(FakeCommand)), BindingKey.Empty);
            
            peerSubscriptionTree.Add(peer, subscription);

            // Act
            var matchingPeers = peerSubscriptionTree.GetPeers(BindingKey.Split(routingKey));

            // Assert
            matchingPeers.Single().ShouldEqual(peer);
        }

        [TestCase("a.b.c")]
        [TestCase("b.c.d")]
        public void stars_should_always_match_if_same_number_of_parts(string routingKey)
        {
            // Arrange
            var peerSubscriptionTree = new PeerSubscriptionTree();
            var peer = new Peer(new PeerId("jesuistonpeer"), "endpoint");
            var subscription = CreateSubscription("*.*.*");

            peerSubscriptionTree.Add(peer, subscription);

            // Act
            var matchingPeers = peerSubscriptionTree.GetPeers(BindingKey.Split(routingKey));

            // Assert
            matchingPeers.Single().ShouldEqual(peer);
        }

        [TestCase("a.b.*")]
        [TestCase("a.*.*")]
        [TestCase("a.*.c")]
        [TestCase("*.b.c")]
        public void binding_key_with_star_should_match_routing_key(string bindingKey)
        {
            // Arrange
            var peerSubscriptionTree = new PeerSubscriptionTree();
            var peer = new Peer(new PeerId("jesuistonpeer"), "endpoint");
            var subscription = CreateSubscription(bindingKey);

            peerSubscriptionTree.Add(peer, subscription);

            // Act
            var matchingPeers = peerSubscriptionTree.GetPeers(BindingKey.Split(bindingKey));

            // Assert
            matchingPeers.Single().ShouldEqual(peer);
        }

        [TestCase("a.b.#")]
        [TestCase("a.#")]
        public void binding_key_with_dashr_should_match_routing_key(string bindingKey)
        {
            // Arrange
            var peerSubscriptionTree = new PeerSubscriptionTree();
            var peer = new Peer(new PeerId("jesuistonpeer"), "endpoint");
            var subscription = CreateSubscription(bindingKey);

            peerSubscriptionTree.Add(peer, subscription);

            // Act
            var matchingPeers = peerSubscriptionTree.GetPeers(BindingKey.Split("a.b.c"));

            // Assert
            matchingPeers.Single().ShouldEqual(peer);
        }

        [TestCase("#")]
        [TestCase("a.#")]
        [TestCase("a.b.c")]
        [TestCase("a.*")]
        [TestCase("*")]
        [TestCase("a.*.b")]
        public void should_check_for_emptyness(string bindingKey)
        {
            var peerSubscriptionTree = new PeerSubscriptionTree();
            var peer = new Peer(new PeerId("1"), "endpoint");

            peerSubscriptionTree.IsEmpty.ShouldBeTrue();
            peerSubscriptionTree.Add(peer, CreateSubscription(bindingKey));
            var subscription = CreateSubscription("lol");
            peerSubscriptionTree.Add(peer, subscription);
            peerSubscriptionTree.IsEmpty.ShouldBeFalse();
            peerSubscriptionTree.Remove(peer, CreateSubscription(bindingKey));
            peerSubscriptionTree.IsEmpty.ShouldBeFalse();
            peerSubscriptionTree.Remove(peer, subscription);
            peerSubscriptionTree.IsEmpty.ShouldBeTrue();
        }

        [Test]
        public void roundtrip_test()
        {
            // Arrange
            var peerSubscriptionTree = new PeerSubscriptionTree();
            var peer1 = new Peer(new PeerId("1"), "endpoint");
            var peer2 = new Peer(new PeerId("2"), "endpoint");
            var peer3 = new Peer(new PeerId("3"), "endpoint");
            var peer4 = new Peer(new PeerId("4"), "endpoint");
            var peer5 = new Peer(new PeerId("5"), "endpoint");
            var peer6 = new Peer(new PeerId("6"), "endpoint");
            var peer7 = new Peer(new PeerId("7"), "endpoint");
            var peer8 = new Peer(new PeerId("8"), "endpoint");
            var peer9 = new Peer(new PeerId("9"), "endpoint");
            var peer0 = new Peer(new PeerId("0"), "endpoint");

            peerSubscriptionTree.Add(peer1, CreateSubscription("#"));
            peerSubscriptionTree.Add(peer2, CreateSubscription("a.b"));
            peerSubscriptionTree.Add(peer3, CreateSubscription("a.*"));
            peerSubscriptionTree.Add(peer4, CreateSubscription("b.*.c"));
            peerSubscriptionTree.Add(peer5, CreateSubscription("b.*.f"));
            peerSubscriptionTree.Add(peer6, CreateSubscription("d.*.c"));
            peerSubscriptionTree.Add(peer7, CreateSubscription("a"));
            peerSubscriptionTree.Add(peer8, CreateSubscription("*.*"));
            peerSubscriptionTree.Add(peer9, CreateSubscription("a.#"));
            peerSubscriptionTree.Add(peer0, CreateSubscription("*"));

            // Act - Assert
            var peers = peerSubscriptionTree.GetPeers(BindingKey.Split("b.1.c"));
            peers.Count.ShouldEqual(2);
            peers.ShouldContain(peer1);
            peers.ShouldContain(peer4);

            peers = peerSubscriptionTree.GetPeers(BindingKey.Split("a.1"));
            peers.Count.ShouldEqual(4);
            peers.ShouldContain(peer1);
            peers.ShouldContain(peer3);
            peers.ShouldContain(peer8);
            peers.ShouldContain(peer9);

            peers = peerSubscriptionTree.GetPeers(BindingKey.Split("a"));
            peers.Count.ShouldEqual(3);
            peers.ShouldContain(peer1);
            peers.ShouldContain(peer7);
            peers.ShouldContain(peer0);
        }

        [TestCase("a.b", "a.b.c.d")]
        [TestCase("d.*", "a.b.c.d")]
        [TestCase("d.#", "a.b.c.d")]
        public void should_not_match_binding_key(string routingKey, string bindingKey)
        {
            // Arrange
            var peerSubscriptionTree = new PeerSubscriptionTree();
            var peer = new Peer(new PeerId("jesuistonpeer"), "endpoint");
            var subscription = CreateSubscription(bindingKey);

            peerSubscriptionTree.Add(peer, subscription);

            // Act
            var matchingPeers = peerSubscriptionTree.GetPeers(BindingKey.Split(routingKey));

            // Assert
            matchingPeers.ShouldBeEmpty();
        }
    }
}
