using System.Linq;
using Abc.Zebus.Directory;
using Abc.Zebus.Routing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Directory
{
    [TestFixture]
    public partial class PeerSubscriptionTreeTests
    {
        private readonly MessageTypeId _messageTypeId = new MessageTypeId(typeof(FakeCommand));

        [TestCase("whatever")]
        [TestCase("*")]
        [TestCase("#")]
        public void single_star_should_always_match(string routingKey)
        {
            // Arrange
            var peerSubscriptionTree = new PeerSubscriptionTree();
            var peer = new Peer(new PeerId("jesuistonpeer"), "endpoint");
            var subscription = BindingKey.Split("*");
            peerSubscriptionTree.Add(peer, subscription);

            // Act
            var matchingPeers = peerSubscriptionTree.GetPeers(BindingKey.Split(routingKey));

            // Assert
            matchingPeers.Single().ShouldEqual(peer);
        }

        [Test]
        public void empty_subscription_key_should_match_empty_routing_key()
        {
            var peerSubscriptionTree = new PeerSubscriptionTree();
            var peer = new Peer(new PeerId("Abc.Testing.0"), "tcp://test:123");
            peerSubscriptionTree.Add(peer, BindingKey.Empty);

            var matchingPeer = peerSubscriptionTree.GetPeers(BindingKey.Empty).ExpectedSingle();
            matchingPeer.Id.ShouldEqual(peer.Id);
        }

        [Test]
        public void simple_subscription_key_should_simple_routing_key()
        {
            var peerSubscriptionTree = new PeerSubscriptionTree();
            var peer = new Peer(new PeerId("Abc.Testing.0"), "tcp://test:123");
            peerSubscriptionTree.Add(peer, new BindingKey("a"));

            var matchingPeer = peerSubscriptionTree.GetPeers(new BindingKey("a")).ExpectedSingle();
            matchingPeer.Id.ShouldEqual(peer.Id);
        }

        [TestCase("whatever")]
        [TestCase("*")]
        [TestCase("#")]
        public void empty_bindingkey_should_always_match(string routingKey)
        {
            // Arrange
            var peerSubscriptionTree = new PeerSubscriptionTree();
            var peer = new Peer(new PeerId("jesuistonpeer"), "endpoint");

            peerSubscriptionTree.Add(peer, BindingKey.Empty);

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
            var subscription = BindingKey.Split("*.*.*");

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
        public void binding_key_with_star_should_match_routing_key(string subscriptionKey)
        {
            // Arrange
            var peerSubscriptionTree = new PeerSubscriptionTree();
            var peer = new Peer(new PeerId("jesuistonpeer"), "endpoint");
            var subscription = BindingKey.Split(subscriptionKey);

            peerSubscriptionTree.Add(peer, subscription);

            // Act
            var matchingPeers = peerSubscriptionTree.GetPeers(new BindingKey("a", "b", "c"));

            // Assert
            matchingPeers.Single().ShouldEqual(peer);
        }

        [TestCase("a.b.#")]
        [TestCase("a.#")]
        public void binding_key_with_dashr_should_match_routing_key(string subscriptionKey)
        {
            // Arrange
            var peerSubscriptionTree = new PeerSubscriptionTree();
            var peer = new Peer(new PeerId("jesuistonpeer"), "endpoint");
            var subscription = BindingKey.Split(subscriptionKey);

            peerSubscriptionTree.Add(peer, subscription);

            // Act
            var matchingPeers = peerSubscriptionTree.GetPeers(new BindingKey("a", "b", "c"));

            // Assert
            matchingPeers.Single().ShouldEqual(peer);
        }

        [TestCase("#")]
        [TestCase("a.#")]
        [TestCase("a.b.c")]
        [TestCase("a.*")]
        [TestCase("*")]
        [TestCase("a.*.b")]
        public void should_check_for_emptyness(string subscriptionKey)
        {
            var peerSubscriptionTree = new PeerSubscriptionTree();
            var peer = new Peer(new PeerId("1"), "endpoint");

            peerSubscriptionTree.IsEmpty.ShouldBeTrue();
            peerSubscriptionTree.Add(peer, BindingKey.Split(subscriptionKey));
            var subscription = BindingKey.Split("lol");
            peerSubscriptionTree.Add(peer, subscription);
            peerSubscriptionTree.IsEmpty.ShouldBeFalse();
            peerSubscriptionTree.Remove(peer, BindingKey.Split(subscriptionKey));
            peerSubscriptionTree.IsEmpty.ShouldBeFalse();
            peerSubscriptionTree.Remove(peer, subscription);
            peerSubscriptionTree.IsEmpty.ShouldBeTrue();
        }

        [Test]
        public void should_ignore_duplicates()
        {
            var peerSubscriptionTree = new PeerSubscriptionTree();
            var peer = new Peer(new PeerId("1"), "endpoint");

            peerSubscriptionTree.Add(peer, BindingKey.Empty);
            peerSubscriptionTree.Add(peer, BindingKey.Empty);
            peerSubscriptionTree.Add(peer, new BindingKey("123"));
            peerSubscriptionTree.Add(peer, new BindingKey("123"));
            peerSubscriptionTree.Add(peer, new BindingKey("123.456"));
            peerSubscriptionTree.Add(peer, new BindingKey("123.456"));

            var peers = peerSubscriptionTree.GetPeers(BindingKey.Empty);
            peers.Count.ShouldEqual(1);

            peerSubscriptionTree.Remove(peer, BindingKey.Empty);
            peerSubscriptionTree.IsEmpty.ShouldBeFalse();
            peerSubscriptionTree.Remove(peer, new BindingKey("123"));
            peerSubscriptionTree.IsEmpty.ShouldBeFalse();
            peerSubscriptionTree.Remove(peer, new BindingKey("123.456"));
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

            peerSubscriptionTree.Add(peer1, BindingKey.Split("#"));
            peerSubscriptionTree.Add(peer2, BindingKey.Split("a.b"));
            peerSubscriptionTree.Add(peer3, BindingKey.Split("a.*"));
            peerSubscriptionTree.Add(peer4, BindingKey.Split("b.*.c"));
            peerSubscriptionTree.Add(peer5, BindingKey.Split("b.*.f"));
            peerSubscriptionTree.Add(peer6, BindingKey.Split("d.*.c"));
            peerSubscriptionTree.Add(peer7, BindingKey.Split("a"));
            peerSubscriptionTree.Add(peer8, BindingKey.Split("*.*"));
            peerSubscriptionTree.Add(peer9, BindingKey.Split("a.#"));
            peerSubscriptionTree.Add(peer0, BindingKey.Split("*"));

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

        [TestCase("a", "d")]
        [TestCase("*.a", "a")]
        [TestCase("*.a", "a.b.a")]
        [TestCase("a.b", "a.d")]
        [TestCase("a.b", "a.d.c")]
        [TestCase("a.b", "a.b.c.d")]
        [TestCase("d.*", "a.b.c.d")]
        [TestCase("d.#", "a.b.c.d")]
        public void invalid_subscription_should_not_match_routing_key(string subscriptionKey, string routingKey)
        {
            // Arrange
            var peerSubscriptionTree = new PeerSubscriptionTree();
            var peer = new Peer(new PeerId("jesuistonpeer"), "endpoint");
            var subscription = BindingKey.Split(subscriptionKey);

            peerSubscriptionTree.Add(peer, subscription);

            // Act
            var matchingPeers = peerSubscriptionTree.GetPeers(BindingKey.Split(routingKey));

            // Assert
            matchingPeers.ShouldBeEmpty();
        }
    }
}