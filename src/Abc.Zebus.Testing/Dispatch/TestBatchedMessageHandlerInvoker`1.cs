using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Dispatch;

namespace Abc.Zebus.Testing.Dispatch
{
    public class TestBatchedMessageHandlerInvoker<TMessage> : BatchedMessageHandlerInvoker where TMessage : class, IEvent
    {
        public TestBatchedMessageHandlerInvoker(bool shouldBeSubscribedOnStartup = true) : base(null, typeof(Handler), typeof(TMessage), shouldBeSubscribedOnStartup)
        {
        }

        public bool Invoked { get; private set; }

        public override void InvokeMessageHandler(IMessageHandlerInvocation invocation)
        {
            Invoked = true;

            using (invocation.SetupForInvocation())
            {
                var message = invocation.Messages.OfType<IExecutableMessage>().FirstOrDefault();
                message?.Execute(invocation);
            }
        }

        public class Handler : IBatchMessageHandler<TMessage>
        {
            public void Handle(List<TMessage> messages)
            {
                throw new NotSupportedException();
            }
        }
    }
}