using System;
using Microsoft.Extensions.Logging;

namespace Abc.Zebus.Serialization
{
    public class MessageSerializer : IMessageSerializer
    {
        private static readonly ILogger _log = ZebusLogManager.GetLogger(typeof(MessageSerializer));

        public IMessage? Deserialize(MessageTypeId messageTypeId, ReadOnlyMemory<byte> stream)
        {
            var messageType = messageTypeId.GetMessageType();
            if (messageType != null)
                return (IMessage)Serializer.Deserialize(messageType, stream);

            _log.LogWarning($"Could not find message type: {messageTypeId.FullName}");
            return null;
        }

        public ReadOnlyMemory<byte> Serialize(IMessage message)
        {
            return Serializer.Serialize(message);
        }

        public bool TryClone(IMessage message, out IMessage clone)
            => Serializer.TryClone(message, out clone!);
    }
}
