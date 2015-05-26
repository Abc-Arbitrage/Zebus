using System;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Testing.Transport;
using Abc.Zebus.Tests.Messages;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Core
{
    public partial class BusTests
    {
        [Test]
        public void should_send_message()
        {
            using (MessageId.PauseIdGeneration())
            {
                var command = new FakeCommand(456);
                SetupPeersHandlingMessage<FakeCommand>(_peerUp);

                _bus.Start();
                _bus.Send(command);

                var sentMessage = _transport.Messages.Single();

                var expectedTransportMessage = command.ToTransportMessage(_self);
                sentMessage.TransportMessage.ShouldHaveSamePropertiesAs(expectedTransportMessage);
                var destination = sentMessage.Targets.Single();
                destination.ShouldHaveSamePropertiesAs(_peerUp);
            }
        }

        [Test]
        public void should_not_send_message_when_bus_is_not_running()
        {
            var exception = Assert.Throws<InvalidOperationException>(() => _bus.Send(new FakeCommand(42)));
            exception.Message.ShouldContain("not running");
        }

        [Test]
        public void should_throw_exception_if_several_peers_can_handle_a_command()
        {
            var command = new FakeCommand(456);

            var peer1 = new Peer(new PeerId("Abc.Testing.Peer1"), "Peer1Endpoint");
            var peer2 = new Peer(new PeerId("Abc.Testing.Peer2"), "Peer2Endpoint");

            SetupPeersHandlingMessage<FakeCommand>(peer1, peer2);

            _bus.Start();

            Assert.Throws<InvalidOperationException>(() => _bus.Send(command));
        }

        [Test]
        public void should_throw_exception_if_no_peer_is_setup_to_handle_a_command()
        {
            var command = new FakeCommand(456);

            SetupPeersHandlingMessage<FakeCommand>(new Peer[0]);

            _bus.Start();

            Assert.Throws<InvalidOperationException>(() => _bus.Send(command));
        }

        [Test]
        public void should_not_consider_if_peer_is_up_to_send_commands([Values(true, false)]bool isTargetPeerUp)
        {
            using (MessageId.PauseIdGeneration())
            {
                var command = new FakeCommand(456);
                SetupPeersHandlingMessage<FakeCommand>(isTargetPeerUp ? _peerUp : _peerDown);

                _bus.Start();
                _bus.Send(command);

                _transport.ExpectExactly(new TransportMessageSent(command.ToTransportMessage(_self), new[] { isTargetPeerUp ? _peerUp : _peerDown }));
            }
        }

        [Test]
        public void should_not_throw_when_handling_a_persistent_command_even_if_peer_is_not_responding()
        {
            var command = new FakeCommand(456);
            var peer = new Peer(new PeerId("Abc.Testing.Peer1"), "Peer1Endpoint", true, isResponding: false);
            SetupPeersHandlingMessage<FakeCommand>(new[] { peer });

            _bus.Start();
            _bus.Send(command);

            var sentMessage = _transport.MessagesSent.ExpectedSingle();
            sentMessage.ShouldHaveSamePropertiesAs(command);
        }

        [Test]
        public void should_throw_when_sending_a_transient_command_to_a_non_responding_peer()
        {
            var command = new FakeNonPersistentCommand(456);
            var peer = new Peer(new PeerId("Abc.Testing.Peer1"), "Peer1Endpoint", true, isResponding: false);
            SetupPeersHandlingMessage<FakeNonPersistentCommand>(new[] { peer });

            _bus.Start();

            Assert.Throws<InvalidOperationException>(() => _bus.Send(command));
        }

        [Test]
        public void should_not_throw_when_sending_a_transient_infrastructure_command_to_a_non_responding_peer()
        {
            var command = new FakeInfrastructureTransientCommand();
            var peer = new Peer(new PeerId("Abc.Testing.Peer1"), "Peer1Endpoint", true, isResponding: false);
            SetupPeersHandlingMessage<FakeInfrastructureTransientCommand>(new[] { peer });

            _bus.Start();

            Assert.DoesNotThrow(() => _bus.Send(command));
        }

        [Test]
        public void should_handle_command_locally_if_several_peers_can_handle_a_command_and_one_of_them_is_self()
        {
            var command = new FakeCommand(456);
            var handled = false;
            SetupDispatch(command, _ => handled = true);

            var otherPeer = new Peer(new PeerId("Abc.Testing.Peer1"), "Peer1Endpoint");
            SetupPeersHandlingMessage<FakeCommand>(otherPeer, _self);

            _bus.Start();
            _bus.Send(command);

            handled.ShouldBeTrue();
            _transport.Messages.ShouldBeEmpty();
        }
       
    }
}