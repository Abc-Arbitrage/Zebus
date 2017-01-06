using Abc.Zebus.Persistence;
using Abc.Zebus.Transport;

namespace Abc.Zebus.Serialization
{
    public static class MessageSerializerExtensions
    {
        public static TransportMessage ToTransportMessage(this IMessageSerializer serializer, IMessage message, PeerId peerId, string peerEndPoint)
        {
            var persistMessageCommand = message as PersistMessageCommand;
            if (persistMessageCommand != null)
                return ToTransportMessage(persistMessageCommand);

            return new TransportMessage(message.TypeId(), serializer.Serialize(message), peerId, peerEndPoint);
        }

        public static IMessage ToMessage(this IMessageSerializer serializer, TransportMessage transportMessage)
        {
            if (transportMessage.PersistentPeerIds != null && transportMessage.PersistentPeerIds.Count != 0)
                return ToPersistMessageCommand(transportMessage);

            return serializer.Deserialize(transportMessage.MessageTypeId, transportMessage.Content);
        }

        private static TransportMessage ToTransportMessage(PersistMessageCommand persistMessageCommand)
        {
            var targetMessage = persistMessageCommand.TransportMessage;
            return targetMessage.WithPersistentPeerIds(persistMessageCommand.Targets);
        }

        private static IMessage ToPersistMessageCommand(TransportMessage transportMessage)
        {
            var targetTransportMessage = new TransportMessage
            {
                Id = transportMessage.Id,
                MessageTypeId = transportMessage.MessageTypeId,
                Content = transportMessage.Content,
                Originator = transportMessage.Originator,
                Environment = transportMessage.Environment,
                WasPersisted = transportMessage.WasPersisted,
            };
            return new PersistMessageCommand(targetTransportMessage, transportMessage.PersistentPeerIds);
        }
    }
}