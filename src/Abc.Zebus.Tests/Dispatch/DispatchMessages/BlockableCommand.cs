using System;
using System.Threading;
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
    }
}