using System.Threading;
using System.Threading.Tasks;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    public class CapturingTaskSchedulerSyncCommandHandler : IMessageHandler<DispatchCommand>
    {
        public TaskScheduler TaskScheduler;
        public readonly EventWaitHandle Signal = new ManualResetEvent(false);

        public void Handle(DispatchCommand message)
        {
            Task.Run(() =>
            {
                TaskScheduler = TaskScheduler.Current;
                Signal.Set();
            });
        }
    }
}
