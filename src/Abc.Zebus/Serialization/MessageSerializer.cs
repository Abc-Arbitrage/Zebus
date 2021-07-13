using System.IO;
using Microsoft.Extensions.Logging;

namespace Abc.Zebus.Serialization
{
    public class MessageSerializer : IMessageSerializer
    {
        private static readonly ILogger _log = ZebusLogManager.GetLogger(typeof(MessageSerializer));

        public IMessage? Deserialize(MessageTypeId messageTypeId, Stream stream)
        {
            var messageType = messageTypeId.GetMessageType();
            if (messageType != null)
                return (IMessage)Serializer.Deserialize(messageType, stream);

            _log.LogWarning($"Could not find message type: {messageTypeId.FullName}");
            return null;
        }

        public Stream Serialize(IMessage message)
        {
            var stream = new MemoryStream();
            Serializer.Serialize(stream, message);
            stream.Position = 0;

            return stream;
        }

        public bool TryClone(IMessage message, out IMessage clone)
            => Serializer.TryClone(message, out clone!);
    }
}
