using System;
using System.Threading.Tasks;
using Abc.Zebus.Dispatch;

namespace Abc.Zebus.Testing.Dispatch
{
    public class TestAsyncMessageHandlerInvoker<TMessage> : MessageHandlerInvoker
        where TMessage : class, IMessage
    {
        public TestAsyncMessageHandlerInvoker(bool shouldBeSubscribedOnStartup = true)
            : base(typeof(Handler), typeof(TMessage), shouldBeSubscribedOnStartup)
        {
        }

        public bool Invoked { get; private set; }
        public override MessageHandlerInvokerMode Mode => MessageHandlerInvokerMode.Asynchronous;

        public override async Task InvokeMessageHandlerAsync(IMessageHandlerInvocation invocation)
        {
            Invoked = true;

            using (invocation.SetupForInvocation())
            {
                foreach (var message in invocation.Messages)
                {
                    (message as IExecutableMessage)?.Execute(invocation);

                    var asyncTask = (message as IAsyncExecutableMessage)?.ExecuteAsync(invocation);
                    if (asyncTask != null)
                        await asyncTask.ConfigureAwait(false);
                }
            }
        }

        public override void InvokeMessageHandler(IMessageHandlerInvocation invocation)
        {
            throw new NotSupportedException();
        }

        public class Handler : IAsyncMessageHandler<TMessage>
        {
            public Task Handle(TMessage message)
            {
                throw new NotSupportedException();
            }
        }
    }
}
