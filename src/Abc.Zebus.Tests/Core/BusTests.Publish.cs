using System;
using System.Linq;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Testing.Transport;
using Abc.Zebus.Tests.Messages;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Core
{
    public partial class BusTests
    {
        public class Publish : BusTests
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

                    _transport.ExpectExactly(new TransportMessageSent(@event.ToTransportMessage(_self), new[] { _peerUp, _peerDown }));
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
                    var destination = sentMessage.Targets.Single();
                    destination.ShouldHaveSamePropertiesAs(_peerUp);
                }
            }

            [Test]
            public void should_publish_a_message_to_specific_peer()
            {
                using (MessageId.PauseIdGeneration())
                {
                    // Arrange
                    var message = new FakeEvent(42);
                    var anotherPeerUp = new Peer(new PeerId("Abc.Testing.other"), "somePeer");
                    SetupPeersHandlingMessage<FakeEvent>(_peerUp, anotherPeerUp);
                    var expectedTransportMessage = message.ToTransportMessage(_self);

                    // Act
                    _bus.Start();
                    _bus.Publish(message, _peerUp.Id);

                    // Assert

                    var sentMessage = _transport.Messages.ExpectedSingle();
                    expectedTransportMessage.ShouldHaveSamePropertiesAs(sentMessage.TransportMessage);
                    var destination = sentMessage.Targets.ExpectedSingle();
                    destination.ShouldHaveSamePropertiesAs(_peerUp);
                }
            }

            [Test]
            public void should_publish_a_message_to_All_peers()
            {
                using (MessageId.PauseIdGeneration())
                {
                    // Arrange
                    var message = new FakeEvent(42);
                    var anotherPeerUp = new Peer(new PeerId("Abc.Testing.other"), "somePeer");
                    SetupPeersHandlingMessage<FakeEvent>(_peerUp, anotherPeerUp);
                    var expectedTransportMessage = message.ToTransportMessage(_self);

                    // Act
                    _bus.Start();
                    _bus.Publish(message);

                    // Assert

                    var sentMessage = _transport.Messages.ExpectedSingle();
                    expectedTransportMessage.ShouldHaveSamePropertiesAs(sentMessage.TransportMessage);
                    sentMessage.Targets.Count.ShouldEqual(2);
                    var destination1 = sentMessage.Targets.ExpectedSingle(x => x.Id == _peerUp.Id);
                    destination1.ShouldHaveSamePropertiesAs(_peerUp);
                    var destination2 = sentMessage.Targets.ExpectedSingle(x => x.Id == anotherPeerUp.Id);
                    destination2.ShouldHaveSamePropertiesAs(anotherPeerUp);


                }
            }
        }
    }
}
