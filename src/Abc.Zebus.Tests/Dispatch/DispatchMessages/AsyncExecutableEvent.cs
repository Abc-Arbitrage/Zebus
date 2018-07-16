using System;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Testing.Dispatch;
using ProtoBuf;

namespace Abc.Zebus.Tests.Dispatch.DispatchMessages
{
    [ProtoContract]
    public class AsyncExecutableEvent : ICommand, IAsyncExecutableMessage
    {
        public Func<IMessageHandlerInvocation, Task> Callback { get; set; }
        public ManualResetEventSlim HandleStarted { get; } = new ManualResetEventSlim();
        public string DispatchQueueName { get; private set; }

        public async Task ExecuteAsync(IMessageHandlerInvocation invocation)
        {
            HandleStarted.Set();
            DispatchQueueName = DispatchQueue.GetCurrentDispatchQueueName();

            var callbackTask = Callback?.Invoke(invocation);
            if (callbackTask != null)
                await callbackTask.ConfigureAwait(false);
        }
    }
}
