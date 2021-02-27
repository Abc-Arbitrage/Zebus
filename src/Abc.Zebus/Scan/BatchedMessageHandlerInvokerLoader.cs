using System;
using Abc.Zebus.DependencyInjection;
using Abc.Zebus.Dispatch;

namespace Abc.Zebus.Scan
{
    public class BatchedMessageHandlerInvokerLoader : MessageHandlerInvokerLoader
    {
        public BatchedMessageHandlerInvokerLoader(IDependencyInjectionContainerProvider containerProvider)
            : base(containerProvider, typeof(IBatchedMessageHandler<>))
        {
        }

        protected override IMessageHandlerInvoker BuildMessageHandlerInvoker(Type handlerType, Type messageType, bool shouldBeSubscribedOnStartup)
        {
            return new BatchedMessageHandlerInvoker(ContainerProvider, handlerType, messageType, shouldBeSubscribedOnStartup);
        }
    }
}
