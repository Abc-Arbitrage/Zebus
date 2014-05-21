using Abc.Zebus.Dispatch;
using Abc.Zebus.Transport;

namespace Abc.Zebus.Core
{
    public class DefaultStoppingStrategy : IStoppingStrategy
    {
        public void Stop(ITransport transport, IMessageDispatcher messageDispatcher)
        {
            messageDispatcher.Stop();
            transport.Stop();   
        }
    }
}