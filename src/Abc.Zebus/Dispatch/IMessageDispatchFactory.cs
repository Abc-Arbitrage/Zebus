using Abc.Zebus.Transport;

namespace Abc.Zebus.Dispatch;

public interface IMessageDispatchFactory
{
    MessageDispatch? CreateMessageDispatch(TransportMessage transportMessage);
}
