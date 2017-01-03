using System;
using System.Threading;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Testing.Dispatch;
using ProtoBuf;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    [ProtoContract]
    public class ExecutableEvent : ICommand, IExecutableMessage
    {
        private readonly EventWaitHandle _blockingSignal = new AutoResetEvent(false);

        public bool IsBlocking { get; set; }
        public Action<IMessageHandlerInvocation> Callback { get; set; }
        public bool HandleStarted { get; private set; }
        public bool HandleStopped { get; private set; }
        public string DispatchQueueName { get; private set; }

        public void Execute(IMessageHandlerInvocation invocation)
        {
            HandleStarted = true;
            DispatchQueueName = DispatchQueue.GetCurrentDispatchQueueName();

            Callback?.Invoke(invocation);

            if (IsBlocking)
                _blockingSignal.WaitOne();

            HandleStopped = true;
        }

        public void Unblock()
        {
            _blockingSignal.Set();
        }
    }
}