﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Abc.Zebus.DependencyInjection;

namespace Abc.Zebus.Dispatch
{
    public class BatchedMessageHandlerInvoker : MessageHandlerInvoker
    {
        private static readonly MethodInfo _castMethodInfo = typeof(Enumerable).GetMethod(nameof(Enumerable.Cast))!;

        private readonly IMessageHandlerContainer _container;
        private readonly Action<object, IList<IMessage>> _handleAction;
        private static readonly MethodInfo _toListMethodInfo = typeof(Enumerable).GetMethod(nameof(Enumerable.ToList))!;

        public BatchedMessageHandlerInvoker(IDependencyInjectionContainerProvider containerProvider, Type handlerType, Type messageType, bool shouldBeSubscribedOnStartup = true)
            : base(handlerType, messageType, shouldBeSubscribedOnStartup)
        {
            _container = containerProvider  != null ? containerProvider.GetMessageHandlerInstanceProvider(handlerType) : null!;
            _handleAction = GenerateHandleAction(handlerType, messageType);
        }

        public override void InvokeMessageHandler(IMessageHandlerInvocation invocation)
        {
            var handler = CreateHandler(invocation.Context);
            using (invocation.SetupForInvocation(handler))
            {
                _handleAction(handler, invocation.Messages);
            }
        }

        public override bool CanMergeWith(IMessageHandlerInvoker other)
        {
            return other is BatchedMessageHandlerInvoker otherBatchedInvoker
                   && otherBatchedInvoker.MessageHandlerType == MessageHandlerType
                   && otherBatchedInvoker.MessageType == MessageType;
        }

        private object CreateHandler(MessageContext messageContext)
        {
            return CreateHandler(_container, messageContext);
        }

        private static Action<object, IList<IMessage>> GenerateHandleAction(Type handlerType, Type messageType)
        {
            var handleMethod = GetHandleMethodOrThrow(handlerType, messageType);
            ThrowIfAsyncVoid(handlerType, handleMethod);

            var handler = Expression.Parameter(typeof(object), "handler");
            var messages = Expression.Parameter(typeof(IList<IMessage>), "messages");

            var castMethod = _castMethodInfo.MakeGenericMethod(messageType);
            var toListMethod = _toListMethodInfo.MakeGenericMethod(messageType);
            var messagesList = Expression.Call(toListMethod, Expression.Call(castMethod, messages));
            var body = Expression.Call(Expression.Convert(handler, handlerType), handleMethod, messagesList);

            var lambda = Expression.Lambda(typeof(Action<object, IList<IMessage>>), body, handler, messages);

            return (Action<object, IList<IMessage>>)lambda.Compile();
        }

        private static MethodInfo GetHandleMethodOrThrow(Type handlerType, Type messageType)
        {
            try
            {
                var interfaceType = typeof(IBatchedMessageHandler<>).MakeGenericType(messageType);
                var interfaceMethod = interfaceType.GetMethod(nameof(IBatchedMessageHandler<IEvent>.Handle), new[] { typeof(IList<>).MakeGenericType(messageType) });
                var interfaceMap = handlerType.GetInterfaceMap(interfaceType);
                var handleIndex = Array.IndexOf(interfaceMap.InterfaceMethods, interfaceMethod);
                return interfaceMap.TargetMethods[handleIndex];
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"The given handler type ({handlerType.Name}) is not an {nameof(IBatchedMessageHandler<IEvent>)}<{messageType.Name}>", ex);
            }
        }
    }
}
