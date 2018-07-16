using System.Threading.Tasks;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    public class AsyncDoNotStartTaskCommandHandler : IAsyncMessageHandler<AsyncDoNotStartTaskCommand>
    {
        public Task Handle(AsyncDoNotStartTaskCommand message)
        {
            return new Task(() => {});
        }
    }
}