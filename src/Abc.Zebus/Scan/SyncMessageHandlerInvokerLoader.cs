using System;
using Abc.Zebus.DependencyInjection;
using Abc.Zebus.Dispatch;
using StructureMap;

namespace Abc.Zebus.Scan
{
    public class SyncMessageHandlerInvokerLoader : MessageHandlerInvokerLoader
    {
        public SyncMessageHandlerInvokerLoader(IDependencyInjectionContainerProvider containerProvider)
            : base(containerProvider, typeof(IMessageHandler<>))
        {
        }

        protected override IMessageHandlerInvoker BuildMessageHandlerInvoker(Type handlerType, Type messageType, bool shouldBeSubscribedOnStartup)
        {
            return new SyncMessageHandlerInvoker(ContainerProvider, handlerType, messageType, shouldBeSubscribedOnStartup);
        }
    }
}
