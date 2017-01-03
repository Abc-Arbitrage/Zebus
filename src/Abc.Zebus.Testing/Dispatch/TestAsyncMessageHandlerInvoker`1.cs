using System;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Dispatch;

namespace Abc.Zebus.Testing.Dispatch
{
    public class TestAsyncMessageHandlerInvoker<TMessage> : MessageHandlerInvoker where TMessage : class, IMessage
    {
        public TestAsyncMessageHandlerInvoker(bool shouldBeSubscribedOnStartup = true) : base(typeof(Handler), typeof(TMessage), shouldBeSubscribedOnStartup)
        {
        }

        public bool Invoked { get; private set; }
        public override MessageHandlerInvokerMode Mode => MessageHandlerInvokerMode.Asynchronous;

        public override Task InvokeMessageHandlerAsync(IMessageHandlerInvocation invocation)
        {
            return Task.Run(() =>
            {
                Invoked = true;
                using (invocation.SetupForInvocation())
                {
                    foreach (var message in invocation.Messages.OfType<IExecutableMessage>())
                    {
                        message.Execute(invocation);
                    }
                }
            });
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