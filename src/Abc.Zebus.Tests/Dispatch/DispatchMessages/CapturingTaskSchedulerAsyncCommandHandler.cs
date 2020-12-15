using System.Threading;
using System.Threading.Tasks;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    public class CapturingTaskSchedulerAsyncCommandHandler : IAsyncMessageHandler<DispatchCommand>
    {
        public TaskScheduler TaskScheduler;
        public readonly EventWaitHandle Signal = new ManualResetEvent(false);

        public Task Handle(DispatchCommand message)
        {
            return Task.Run(() =>
            {
                TaskScheduler = TaskScheduler.Current;
                Signal.Set();
            });
        }
    }
}
