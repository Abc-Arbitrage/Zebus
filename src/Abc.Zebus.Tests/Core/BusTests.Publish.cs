using System;
using System.Linq;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Testing.Transport;
using Abc.Zebus.Tests.Messages;
using Abc.Zebus.Transport;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Core
{
    public partial class BusTests
    {
        [Test]
        public void should_not_consider_if_peer_is_up_to_publish_events()
        {
            using (MessageId.PauseIdGeneration())
            {
                var @event = new FakeEvent(456);
                SetupPeersHandlingMessage<FakeEvent>(_peerUp, _peerDown);

                _bus.Start();
                _bus.Publish(@event);

                _transport.ExpectExactly(new TransportMessageSent(@event.ToTransportMessage(_self), new[] { new PeerWithPersistenceInfo(_peerUp, false), new PeerWithPersistenceInfo(_peerDown, false) }));
            }
        }

        [Test]
        public void should_not_publish_message_when_bus_is_not_running()
        {
            var exception = Assert.Throws<InvalidOperationException>(() => _bus.Publish(new FakeEvent(42)));
            exception.Message.ShouldContain("not running");
        }

        [Test]
        public void should_publish_a_message()
        {
            using (MessageId.PauseIdGeneration())
            {
                var message = new FakeEvent(456);
                SetupPeersHandlingMessage<FakeEvent>(_peerUp);
                var expectedTransportMessage = message.ToTransportMessage(_self);

                _bus.Start();
                _bus.Publish(message);

                var sentMessage = _transport.Messages.Single();
                expectedTransportMessage.ShouldHaveSamePropertiesAs(sentMessage.TransportMessage);
                var destination = sentMessage.Targets.Single().Peer;
                destination.ShouldHaveSamePropertiesAs(_peerUp);
            }
        }
    }
}