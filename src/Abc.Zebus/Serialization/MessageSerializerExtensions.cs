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
            if (transportMessage.IsPersistTransportMessage)
                return ToPersistMessageCommand(transportMessage);

            return serializer.Deserialize(transportMessage.MessageTypeId, transportMessage.Content);
        }

        private static TransportMessage ToTransportMessage(PersistMessageCommand persistMessageCommand)
            => persistMessageCommand.TransportMessage.ToPersistTransportMessage(persistMessageCommand.Targets);

        private static IMessage ToPersistMessageCommand(TransportMessage transportMessage)
            => new PersistMessageCommand(transportMessage.UnpackPersistTransportMessage(), transportMessage.PersistentPeerIds);
    }
}
