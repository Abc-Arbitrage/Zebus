using System;
using Abc.Zebus.DependencyInjection;
using Abc.Zebus.Dispatch;

namespace Abc.Zebus.Scan
{
    public class AsyncMessageHandlerInvokerLoader : MessageHandlerInvokerLoader
    {
        public AsyncMessageHandlerInvokerLoader(IDependencyInjectionContainerProvider containerProvider)
            : base(containerProvider, typeof(IAsyncMessageHandler<>))
        {
        }

        protected override IMessageHandlerInvoker BuildMessageHandlerInvoker(Type handlerType, Type messageType, bool shouldBeSubscribedOnStartup)
        {
            return new AsyncMessageHandlerInvoker(ContainerProvider, handlerType, messageType, shouldBeSubscribedOnStartup);
        }
    }
}
