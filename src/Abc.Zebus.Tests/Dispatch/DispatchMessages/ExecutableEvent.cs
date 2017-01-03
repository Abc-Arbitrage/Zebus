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
        private readonly ManualResetEventSlim _blockingSignal = new ManualResetEventSlim();

        public bool IsBlocking { get; set; }
        public Action<IMessageHandlerInvocation> Callback { get; set; }
        public ManualResetEventSlim HandleStarted { get; } = new ManualResetEventSlim();
        public string DispatchQueueName { get; private set; }

        public void Execute(IMessageHandlerInvocation invocation)
        {
            HandleStarted.Set();
            DispatchQueueName = DispatchQueue.GetCurrentDispatchQueueName();

            Callback?.Invoke(invocation);

            if (IsBlocking)
                _blockingSignal.Wait();
        }

        public void Unblock()
        {
            _blockingSignal.Set();
        }
    }
}