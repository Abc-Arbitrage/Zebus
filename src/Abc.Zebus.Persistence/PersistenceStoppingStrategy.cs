using Abc.Zebus.Core;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Transport;

namespace Abc.Zebus.Persistence
{
    public class PersistenceStoppingStrategy : IStoppingStrategy
    {
        public void Stop(ITransport transport, IMessageDispatcher messageDispatcher)
        {
            transport.Stop();
            messageDispatcher.Stop();
        }
    }
}