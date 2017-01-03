using System;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Testing.Extensions;

namespace Abc.Zebus.Testing.Dispatch
{
    public class TestMessageHandlerInvoker<TMessage> : MessageHandlerInvoker where TMessage : class, IMessage
    {
        public TestMessageHandlerInvoker(bool shouldBeSubscribedOnStartup = true) : base(typeof(Handler), typeof(TMessage), shouldBeSubscribedOnStartup)
        {
        }

        public bool Invoked { get; private set; }

        public override void InvokeMessageHandler(IMessageHandlerInvocation invocation)
        {
            Invoked = true;

            using (invocation.SetupForInvocation())
            {
                var message = invocation.Messages.ExpectedSingle() as IExecutableMessage;
                message?.Execute(invocation);
            }
        }

        public class Handler : IMessageHandler<TMessage>
        {
            public void Handle(TMessage message)
            {
                throw new NotSupportedException();
            }
        }
    }
}