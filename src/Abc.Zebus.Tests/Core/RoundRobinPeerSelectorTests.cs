using Abc.Zebus.Core;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Core
{
    [TestFixture]
    public class RoundRobinPeerSelectorTests
    {
        [Test]
        public void should_return_null_when_no_peer_can_handle_the_command()
        {
            // Arrange
            var resolver = new RoundRobinPeerSelector();
            var command = new FakeCommand(42);
            var handlingPeers = new Peer[0];

            // Act
            var resolvedPeer = resolver.GetTargetPeer(command, handlingPeers);

            // Assert
            resolvedPeer.ShouldBeNull();
        }

        [Test]
        public void should_return_the_only_handling_peer_when_there_is_only_one_handling_peer()
        {
            // Arrange
            var resolver = new RoundRobinPeerSelector();
            var command = new FakeCommand(42);
            var peer1 = new Peer(new PeerId("peer1"), "endpoint1");
            var handlingPeers = new []{ peer1 };

            // Act
            var resolvedPeer = resolver.GetTargetPeer(command, handlingPeers);

            // Assert
            resolvedPeer.ShouldEqual(peer1);
        }

        [Test]
        public void should_resolve_peer_using_basic_round_robin()
        {
            // Arrange
            var resolver = new RoundRobinPeerSelector();
            var command = new FakeCommand(42);
            var peer1 = new Peer(new PeerId("peer1"), "endpoint1");
            var peer2 = new Peer(new PeerId("peer2"), "endpoint2");
            var peer3 = new Peer(new PeerId("peer3"), "endpoint3");
            var handlingPeers = new[] { peer1, peer2, peer3 };

            // Act - Assert
            var resolvedPeer = resolver.GetTargetPeer(command, handlingPeers);
            resolvedPeer.ShouldEqual(peer1);

            resolvedPeer = resolver.GetTargetPeer(command, handlingPeers);
            resolvedPeer.ShouldEqual(peer2);

            resolvedPeer = resolver.GetTargetPeer(command, handlingPeers);
            resolvedPeer.ShouldEqual(peer3);

            resolvedPeer = resolver.GetTargetPeer(command, handlingPeers);
            resolvedPeer.ShouldEqual(peer1);
        }

        [Test]
        public void should_resolve_peer_using_basic_round_robin_for_different_commands()
        {
            // Arrange
            var resolver = new RoundRobinPeerSelector();
            
            var command1 = new FakeCommand(42);
            var command1Peer1 = new Peer(new PeerId("command1peer1"), "endpoint1");
            var command1Peer2 = new Peer(new PeerId("command1peer2"), "endpoint2");
            var command1HandlingPeer = new[] { command1Peer1, command1Peer2 };

            var command2 = new FakeInfrastructureCommand();
            var command2Peer1 = new Peer(new PeerId("command2peer1"), "endpoint1");
            var command2Peer2 = new Peer(new PeerId("command2peer2"), "endpoint2");
            var command2HandlingPeer = new[] { command2Peer1, command2Peer2 };

            // Act - Assert
            var resolvedPeer = resolver.GetTargetPeer(command1, command1HandlingPeer);
            resolvedPeer.ShouldEqual(command1Peer1);

            resolvedPeer = resolver.GetTargetPeer(command1, command1HandlingPeer);
            resolvedPeer.ShouldEqual(command1Peer2);

            resolvedPeer = resolver.GetTargetPeer(command2, command2HandlingPeer);
            resolvedPeer.ShouldEqual(command2Peer1);

            resolvedPeer = resolver.GetTargetPeer(command1, command1HandlingPeer);
            resolvedPeer.ShouldEqual(command1Peer1);

            resolvedPeer = resolver.GetTargetPeer(command2, command2HandlingPeer);
            resolvedPeer.ShouldEqual(command2Peer2);

            resolvedPeer = resolver.GetTargetPeer(command2, command2HandlingPeer);
            resolvedPeer.ShouldEqual(command2Peer1);
        }

        [Test]
        public void should_handle_handling_peers_count_changes()
        {
            // Arrange
            var resolver = new RoundRobinPeerSelector();
            var command = new FakeCommand(42);
            var peer1 = new Peer(new PeerId("peer1"), "endpoint1");
            var peer2 = new Peer(new PeerId("peer2"), "endpoint2");
            var peer3 = new Peer(new PeerId("peer3"), "endpoint3");
            var handlingPeers = new[] { peer1, peer2, peer3 };

            // Act - Assert
            var resolvedPeer = resolver.GetTargetPeer(command, handlingPeers);
            resolvedPeer.ShouldEqual(peer1);
            resolvedPeer = resolver.GetTargetPeer(command, handlingPeers);
            resolvedPeer.ShouldEqual(peer2);

            handlingPeers = new[] {peer1, peer2};

            resolvedPeer = resolver.GetTargetPeer(command, handlingPeers);
            resolvedPeer.ShouldEqual(peer1);
        }
    }
}