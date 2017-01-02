using System;
using System.Threading;
using Abc.Zebus.Dispatch;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    [DispatchQueueName("DispatchQueue1")]
    public class SyncCommandHandlerWithQueueName1 : IMessageHandler<DispatchCommand>
    {
        public readonly EventWaitHandle CalledSignal = new AutoResetEvent(false);
        public bool WaitForSignal;
        public bool HandleStarted;
        public bool HandleStopped;
        public string DispatchQueueName { get; set; }
        public Action Callback;

        public void Handle(DispatchCommand message)
        {
            HandleStarted = true;
            DispatchQueueName = DispatchQueue.GetCurrentDispatchQueueName();

            Callback?.Invoke();

            if (WaitForSignal)
                CalledSignal.WaitOne();

            HandleStopped = true;
        }
    }
}