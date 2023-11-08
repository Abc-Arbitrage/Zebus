using Abc.Zebus.Routing;

namespace Abc.Zebus.Directory;

public readonly struct MessageBinding
{
    public readonly MessageTypeId MessageTypeId;
    public readonly RoutingContent RoutingContent;

    public MessageBinding(MessageTypeId messageTypeId, RoutingContent routingContent)
    {
        MessageTypeId = messageTypeId;
        RoutingContent = routingContent;
    }

    public static MessageBinding FromMessage(IMessage message)
    {
        var messageTypeId = message.TypeId();
        var routingContent = GetRoutingContent(message, messageTypeId);

        return new MessageBinding(messageTypeId, routingContent);
    }

    public static MessageBinding Default<T>()
        where T : IMessage => new(MessageUtil.TypeId<T>(), RoutingContent.Empty);

    private static RoutingContent GetRoutingContent(IMessage message, MessageTypeId messageTypeId)
    {
        var members = messageTypeId.Descriptor.RoutingMembers;
        if (members.Length == 0)
            return RoutingContent.Empty;

        var values = new RoutingContentValue[members.Length];

        for (var tokenIndex = 0; tokenIndex < values.Length; ++tokenIndex)
        {
            values[tokenIndex] = members[tokenIndex].GetValue(message);
        }

        return new RoutingContent(values);
    }
}
