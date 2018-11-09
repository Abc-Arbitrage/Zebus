using System.IO;
using log4net;

namespace Abc.Zebus.Serialization
{
    public class MessageSerializer : IMessageSerializer
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(MessageSerializer));

        public IMessage Deserialize(MessageTypeId messageTypeId, Stream stream)
        {
            var messageType = messageTypeId.GetMessageType();
            if (messageType != null)
                return (IMessage)Serializer.Deserialize(messageType, stream);

            _log.WarnFormat("Could not find message type: {0}", messageTypeId.FullName);
            return null;

        }

        public Stream Serialize(IMessage message)
        {
            var stream = new MemoryStream();
            Serializer.Serialize(stream, message);
            stream.Position = 0;

            return stream;
        }
    }
}
