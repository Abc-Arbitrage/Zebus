using Abc.Zebus.Dispatch;
using Abc.Zebus.Transport;

namespace Abc.Zebus.Core;

public interface IStoppingStrategy
{
    void Stop(ITransport transport, IMessageDispatcher messageDispatcher);
}
