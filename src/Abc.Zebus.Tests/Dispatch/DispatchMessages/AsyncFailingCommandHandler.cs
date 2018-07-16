using System.Threading.Tasks;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    public class AsyncFailingCommandHandler : IAsyncMessageHandler<AsyncFailingCommand>
    {
        public Task Handle(AsyncFailingCommand message)
        {
            if (message.ThrowSynchronously)
                throw message.Exception;

            return Task.Factory.StartNew(() => { throw message.Exception; });
        }
    }
}
