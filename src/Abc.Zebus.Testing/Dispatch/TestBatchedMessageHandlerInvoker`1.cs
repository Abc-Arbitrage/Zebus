using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Dispatch;

namespace Abc.Zebus.Testing.Dispatch;

public class TestBatchedMessageHandlerInvoker<TMessage> : BatchedMessageHandlerInvoker
    where TMessage : class, IEvent
{
    public TestBatchedMessageHandlerInvoker()
        : base(null!, typeof(Handler), typeof(TMessage), MessageHandlerInvokerSubscriber.FromAttributes(typeof(Handler)))
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

    public class Handler : IBatchedMessageHandler<TMessage>
    {
        public void Handle(IList<TMessage> messages)
        {
            throw new NotSupportedException();
        }
    }
}
