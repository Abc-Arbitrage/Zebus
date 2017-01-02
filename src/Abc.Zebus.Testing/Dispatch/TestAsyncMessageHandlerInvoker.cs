using System;
using System.Threading.Tasks;
using Abc.Zebus.Dispatch;

namespace Abc.Zebus.Testing.Dispatch
{
    public class TestAsyncMessageHandlerInvoker : MessageHandlerInvoker
    {
        public TestAsyncMessageHandlerInvoker(Type handlerType, Type messageType, bool shouldBeSubscribedOnStartup = true) 
            : base(handlerType, messageType, shouldBeSubscribedOnStartup)
        {
        }

        public bool Invoked { get; private set; }
        public Action<IMessageHandlerInvocation> InvokeMessageHandlerCallback { get; set; }
        public override MessageHandlerInvokerMode Mode => MessageHandlerInvokerMode.Asynchronous;

        public override Task InvokeMessageHandlerAsync(IMessageHandlerInvocation invocation)
        {
            return Task.Run(() =>
            {
                Invoked = true;
                if (InvokeMessageHandlerCallback == null)
                    return;

                using (invocation.SetupForInvocation())
                {
                    InvokeMessageHandlerCallback(invocation);
                }
            });
        }

        public override void InvokeMessageHandler(IMessageHandlerInvocation invocation)
        {
            throw new NotSupportedException();
        }
    }
}