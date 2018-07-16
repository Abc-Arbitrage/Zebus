using System.Threading;
using Abc.Zebus.Dispatch;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    [DispatchQueueName("DispatchQueue2")]
    public class SyncCommandHandlerWithQueueName2 : IMessageHandler<DispatchCommand>
    {
        public readonly EventWaitHandle CalledSignal = new AutoResetEvent(false);
        public bool WaitForSignal;
        public bool HandleStarted;
        public bool HandleStopped;

        public void Handle(DispatchCommand message)
        {
            HandleStarted = true;

            if (WaitForSignal)
                CalledSignal.WaitOne();

            HandleStopped = true;
        }
    }
}