using System;
using System.Diagnostics.CodeAnalysis;

namespace Abc.Zebus.Serialization;

public interface IMessageSerializer
{
    ReadOnlyMemory<byte> Serialize(IMessage message);
    IMessage? Deserialize(MessageTypeId messageTypeId, ReadOnlyMemory<byte> bytes);
    bool TryClone(IMessage message, [MaybeNullWhen(false)] out IMessage clone);
}
