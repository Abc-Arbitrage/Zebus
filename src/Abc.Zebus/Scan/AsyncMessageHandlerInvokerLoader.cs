using System;
using Abc.Zebus.Dispatch;
using StructureMap;

namespace Abc.Zebus.Scan;

public class AsyncMessageHandlerInvokerLoader : MessageHandlerInvokerLoader
{
    public AsyncMessageHandlerInvokerLoader(IContainer container)
        : base(container, typeof(IAsyncMessageHandler<>))
    {
    }

    protected override IMessageHandlerInvoker BuildMessageHandlerInvoker(Type handlerType, Type messageType, MessageHandlerInvokerSubscriber subscriber)
        => new AsyncMessageHandlerInvoker(Container, handlerType, messageType, subscriber);
}
