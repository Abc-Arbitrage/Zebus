using System;
using System.Threading;
using Abc.Zebus.Dispatch;
using ProtoBuf;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    [ProtoContract]
    public class BlockableCommand : ICommand
    {
        public readonly EventWaitHandle BlockingSignal = new AutoResetEvent(false);
        public bool IsBlocking;
        public bool HandleStarted;
        public bool HandleStopped;
        public string DispatchQueueName { get; set; }
        public Action Callback;

        public void Handle()
        {
            HandleStarted = true;
            DispatchQueueName = DispatchQueue.GetCurrentDispatchQueueName();

            Callback?.Invoke();

            if (IsBlocking)
                BlockingSignal.WaitOne();

            HandleStopped = true;
        }
    }
}