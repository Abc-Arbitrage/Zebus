using System.IO;

namespace Abc.Zebus.Serialization
{
    public class MessageSerializer : IMessageSerializer
    {
        private readonly Serializer _serializer = new Serializer();

        public IMessage Deserialize(MessageTypeId messageTypeId, byte[] messageBytes)
        {
            var messageType = messageTypeId.GetMessageType();
            if (messageType == null)
                return null;

            return (IMessage)_serializer.Deserialize(messageType, new MemoryStream(messageBytes));
        }

        public byte[] Serialize(IMessage message)
        {
            var stream = new MemoryStream();
            _serializer.Serialize(stream, message);

            return stream.ToArray();
        }
    }
}