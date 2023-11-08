﻿using System;
using System.Linq.Expressions;
using System.Reflection;
using StructureMap;

namespace Abc.Zebus.Dispatch;

public class SyncMessageHandlerInvoker : MessageHandlerInvoker
{
    private readonly Action<object, IMessage> _handleAction;

    public SyncMessageHandlerInvoker(IContainer container, Type handlerType, Type messageType)
        : this(container, handlerType, messageType, MessageHandlerInvokerSubscriber.FromAttributes(handlerType))
    {
    }

    public SyncMessageHandlerInvoker(IContainer container, Type handlerType, Type messageType, MessageHandlerInvokerSubscriber subscriber)
        : base(container, handlerType, messageType, subscriber)
    {
        _handleAction = GenerateHandleAction(handlerType, messageType);
    }

    public override void InvokeMessageHandler(IMessageHandlerInvocation invocation)
    {
        var handler = CreateHandler(invocation.Context);
        using (invocation.SetupForInvocation(handler))
        {
            _handleAction(handler, invocation.Messages[0]);
        }
    }

    private static Action<object, IMessage> GenerateHandleAction(Type handlerType, Type messageType)
    {
        var handleMethod = GetHandleMethodOrThrow(handlerType, messageType);
        ThrowIfAsyncVoid(handlerType, handleMethod);

        var o = Expression.Parameter(typeof(object), "o");
        var m = Expression.Parameter(typeof(IMessage), "m");
        var body = Expression.Call(Expression.Convert(o, handlerType), handleMethod, Expression.Convert(m, messageType));
        var lambda = Expression.Lambda(typeof(Action<object, IMessage>), body, o, m);

        return (Action<object, IMessage>)lambda.Compile();
    }

    private static MethodInfo GetHandleMethodOrThrow(Type handlerType, Type messageType)
    {
        try
        {
            var interfaceType = typeof(IMessageHandler<>).MakeGenericType(messageType);
            var interfaceMethod = interfaceType.GetMethod(nameof(IMessageHandler<IMessage>.Handle), new[] { messageType });
            var interfaceMap = handlerType.GetInterfaceMap(interfaceType);
            var handleIndex = Array.IndexOf(interfaceMap.InterfaceMethods, interfaceMethod);
            return interfaceMap.TargetMethods[handleIndex];
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"The given handler type ({handlerType.Name}) is not an {nameof(IMessageHandler<IMessage>)}<{messageType.Name}>", ex);
        }
    }
}
