using System;
using Abc.Zebus.Dispatch;
using StructureMap;

namespace Abc.Zebus.Scan
{
    public class BatchedMessageHandlerInvokerLoader : MessageHandlerInvokerLoader
    {
        public BatchedMessageHandlerInvokerLoader(IContainer container)
            : base(container, typeof(IBatchedMessageHandler<>))
        {
        }

        protected override IMessageHandlerInvoker BuildMessageHandlerInvoker(Type handlerType, Type messageType, bool shouldBeSubscribedOnStartup)
        {
            return new BatchedMessageHandlerInvoker(Container, handlerType, messageType, shouldBeSubscribedOnStartup);
        }
    }
}
