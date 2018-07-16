using System.Collections.Generic;
using Abc.Zebus.Persistence;
using Abc.Zebus.Serialization;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Serialization
{
    [TestFixture]
    public class MessageSerializerTests
    {
        private MessageSerializer _messageSerializer;
        private Peer _self;

        [SetUp]
        public void SetUp()
        {
            _messageSerializer = new MessageSerializer();
            _self = new Peer(new PeerId("Abc.Testing.Self"), "tcp://testing:123");
        }

        [Test]
        public void should_serialize_persist_message_command()
        {
            var transportMessage = new FakeEvent(42).ToTransportMessage(_self);
            var persistMessageCommand = new PersistMessageCommand(transportMessage, new PeerId("Abc.Testing.A"), new PeerId("Abc.Testing.B"));

            var serializedTransportMessage = _messageSerializer.ToTransportMessage(persistMessageCommand, _self.Id, _self.EndPoint);

            serializedTransportMessage.Id.ShouldEqual(transportMessage.Id);
            serializedTransportMessage.MessageTypeId.ShouldEqual(transportMessage.MessageTypeId);
            serializedTransportMessage.Content.ShouldEqual(transportMessage.Content);
            serializedTransportMessage.Originator.ShouldEqual(transportMessage.Originator);
            serializedTransportMessage.Environment.ShouldEqual(transportMessage.Environment);
            serializedTransportMessage.WasPersisted.ShouldEqual(transportMessage.WasPersisted);
            serializedTransportMessage.PersistentPeerIds.ShouldBeEquivalentTo(persistMessageCommand.Targets);
        }

        [Test]
        public void should_deserialize_persist_message_command()
        {
            var transportMessage = new FakeEvent(42).ToTransportMessage(_self);
            transportMessage.PersistentPeerIds = new List<PeerId> { new PeerId("Abc.Testing.A"), new PeerId("Abc.Testing.B") };

            var message = _messageSerializer.ToMessage(transportMessage).ShouldBe<PersistMessageCommand>();

            var deserializedTransportMessage = message.TransportMessage;
            deserializedTransportMessage.Id.ShouldEqual(transportMessage.Id);
            deserializedTransportMessage.MessageTypeId.ShouldEqual(transportMessage.MessageTypeId);
            deserializedTransportMessage.Content.ShouldEqual(transportMessage.Content);
            deserializedTransportMessage.Originator.ShouldEqual(transportMessage.Originator);
            deserializedTransportMessage.Environment.ShouldEqual(transportMessage.Environment);
            deserializedTransportMessage.WasPersisted.ShouldEqual(transportMessage.WasPersisted);

            message.Targets.ShouldBeEquivalentTo(transportMessage.PersistentPeerIds);
        }
    }
}