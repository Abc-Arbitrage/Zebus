using Abc.Zebus.Transport;

namespace Abc.Zebus.Serialization
{
    public static class MessageSerializerExtensions
    {
        public static TransportMessage ToTransportMessage(this IMessageSerializer serializer, IMessage message, MessageId messageId, PeerId peerId, string peerEndPoint)
        {
            return new TransportMessage(message.TypeId(), serializer.Serialize(message), peerId, peerEndPoint, messageId);
        }

        public static IMessage ToMessage(this IMessageSerializer serializer, TransportMessage transportMessage)
        {
            return serializer.Deserialize(transportMessage.MessageTypeId, transportMessage.Content);
        }
    }
}