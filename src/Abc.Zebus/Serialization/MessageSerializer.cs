using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Abc.Zebus.Serialization;

public class MessageSerializer : IMessageSerializer
{
    private static readonly ILogger _log = ZebusLogManager.GetLogger(typeof(MessageSerializer));

    public IMessage? Deserialize(MessageTypeId messageTypeId, ReadOnlyMemory<byte> bytes)
    {
        var messageType = messageTypeId.GetMessageType();
        if (messageType != null)
            return (IMessage)ProtoBufConvert.Deserialize(messageType, bytes);

        _log.LogWarning($"Could not find message type: {messageTypeId.FullName}");
        return null;
    }

    public ReadOnlyMemory<byte> Serialize(IMessage message)
        => ProtoBufConvert.Serialize(message);

    public bool TryClone(IMessage message, out IMessage clone)
    {
        var messageType = message.GetType();
        if (ProtoBufConvert.CanSerialize(messageType))
        {
            // Cannot use the DeepClone method as it doesn't handle classes without a parameterless constructor

            using var ms = new MemoryStream();

            ProtoBufConvert.Serialize(ms, message!);
            ms.Position = 0;
            clone = (IMessage)ProtoBufConvert.Deserialize(messageType, ms);

            return true;
        }

        clone = null!;
        return false;
    }
}
