using System.Threading;
using Abc.Zebus.Dispatch;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    [DispatchQueueName("DispatchQueue1")]
    public class SyncCommandHandlerWithQueueName1 : IMessageHandler<DispatchCommand>, IMessageContextAware
    {
        public readonly EventWaitHandle CalledSignal = new AutoResetEvent(false);
        public bool WaitForSignal;
        public bool HandleStarted;
        public bool HandleStopped;
        public string DispatchQueueName { get; set; }

        public MessageContext Context { get; set; }

        public void Handle(DispatchCommand message)
        {
            HandleStarted = true;
            DispatchQueueName = Context.DispatchQueueName;

            if (WaitForSignal)
                CalledSignal.WaitOne();

            HandleStopped = true;
        }
    }
}