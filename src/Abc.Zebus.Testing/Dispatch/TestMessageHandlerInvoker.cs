using System;
using Abc.Zebus.Dispatch;

namespace Abc.Zebus.Testing.Dispatch
{
    public class TestMessageHandlerInvoker : MessageHandlerInvoker
    {
        public TestMessageHandlerInvoker(Type handlerType, Type messageType, bool shouldBeSubscribedOnStartup = true) 
            : base(handlerType, messageType, shouldBeSubscribedOnStartup)
        {
        }

        public bool Invoked { get; private set; }
        public Action<IMessageHandlerInvocation> InvokeMessageHandlerCallback { get; set; }

        public override void InvokeMessageHandler(IMessageHandlerInvocation invocation)
        {
            Invoked = true;
            if (InvokeMessageHandlerCallback == null)
                return;

            using (invocation.SetupForInvocation())
            {
                InvokeMessageHandlerCallback(invocation);
            }
        }
    }
}