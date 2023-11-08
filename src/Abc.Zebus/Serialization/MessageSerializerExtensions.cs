using System;
using Abc.Zebus.Persistence;
using Abc.Zebus.Transport;

namespace Abc.Zebus.Serialization;

public static class MessageSerializerExtensions
{
    public static TransportMessage ToTransportMessage(this IMessageSerializer serializer, IMessage message, PeerId peerId, string peerEndPoint)
    {
        if (message is PersistMessageCommand persistMessageCommand)
            return ToTransportMessage(persistMessageCommand);

        return new TransportMessage(message.TypeId(), serializer.Serialize(message), peerId, peerEndPoint);
    }

    public static IMessage? ToMessage(this IMessageSerializer serializer, TransportMessage transportMessage)
        => ToMessage(serializer, transportMessage, transportMessage.MessageTypeId, transportMessage.Content);

    public static IMessage? ToMessage(this IMessageSerializer serializer, TransportMessage transportMessage, MessageTypeId messageTypeId, ReadOnlyMemory<byte> content)
    {
        if (transportMessage.IsPersistTransportMessage)
            return ToPersistMessageCommand(transportMessage);

        return serializer.Deserialize(messageTypeId, content);
    }

    private static TransportMessage ToTransportMessage(PersistMessageCommand persistMessageCommand)
        => persistMessageCommand.TransportMessage.ConvertToPersistTransportMessage(persistMessageCommand.Targets);

    private static IMessage ToPersistMessageCommand(TransportMessage transportMessage)
        => new PersistMessageCommand(transportMessage.ConvertFromPersistTransportMessage(), transportMessage.PersistentPeerIds!);
}
