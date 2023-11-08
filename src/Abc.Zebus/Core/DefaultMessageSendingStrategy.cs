using Abc.Zebus.Transport;

namespace Abc.Zebus.Core;

public class DefaultMessageSendingStrategy : IMessageSendingStrategy
{
    public bool IsMessagePersistent(TransportMessage transportMessage) => transportMessage.MessageTypeId.IsPersistent();
}
