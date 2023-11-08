using Abc.Zebus.Transport;

namespace Abc.Zebus.Core;

public interface IMessageSendingStrategy
{
    bool IsMessagePersistent(TransportMessage transportMessage);
}
