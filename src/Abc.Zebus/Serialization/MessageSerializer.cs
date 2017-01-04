using System.IO;

namespace Abc.Zebus.Serialization
{
    public class MessageSerializer : IMessageSerializer
    {
        private readonly Serializer _serializer = new Serializer();

        public IMessage Deserialize(MessageTypeId messageTypeId, Stream stream)
        {
            var messageType = messageTypeId.GetMessageType();
            if (messageType == null)
                return null;

            return (IMessage)_serializer.Deserialize(messageType, stream);
        }

        public Stream Serialize(IMessage message)
        {
            var stream = new MemoryStream();
            _serializer.Serialize(stream, message);
            stream.Position = 0;

            return stream;
        }
    }
}