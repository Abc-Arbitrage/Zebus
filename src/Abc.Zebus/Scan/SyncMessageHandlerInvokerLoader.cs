﻿using System;
using Abc.Zebus.Dispatch;
using StructureMap;

namespace Abc.Zebus.Scan;

public class SyncMessageHandlerInvokerLoader : MessageHandlerInvokerLoader
{
    public SyncMessageHandlerInvokerLoader(IContainer container)
        : base(container, typeof(IMessageHandler<>))
    {
    }

    protected override IMessageHandlerInvoker BuildMessageHandlerInvoker(Type handlerType, Type messageType, MessageHandlerInvokerSubscriber subscriber)
        => new SyncMessageHandlerInvoker(Container, handlerType, messageType, subscriber);
}
