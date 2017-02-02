using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Abc.Zebus.Util.Extensions;
using StructureMap;

namespace Abc.Zebus.Dispatch
{
    public class BatchedMessageHandlerInvoker : MessageHandlerInvoker
    {
        private static readonly MethodInfo _castMethodInfo = typeof(Enumerable).GetMethod("Cast");

        private readonly IContainer _container;
        private readonly Action<object, List<IMessage>> _handleAction;
        private static readonly MethodInfo _toListMethodInfo = typeof(Enumerable).GetMethod("ToList");

        public BatchedMessageHandlerInvoker(IContainer container, Type handlerType, Type messageType, bool shouldBeSubscribedOnStartup = true)
            : base(handlerType, messageType, shouldBeSubscribedOnStartup)
        {
            _container = container;
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
            var otherBatchedInvoker = other as BatchedMessageHandlerInvoker;
            return otherBatchedInvoker != null && otherBatchedInvoker.MessageHandlerType == MessageHandlerType && otherBatchedInvoker.MessageType == MessageType;
        }

        private object CreateHandler(MessageContext messageContext)
        {
            return CreateHandler(_container, messageContext);
        }

        private static Action<object, List<IMessage>> GenerateHandleAction(Type handlerType, Type messageType)
        {
            var handleMethod = GetHandleMethodOrThrow(handlerType, messageType);
            ThrowIfAsyncVoid(handlerType, handleMethod);

            var handler = Expression.Parameter(typeof(object), "handler");
            var messages = Expression.Parameter(typeof(List<IMessage>), "messages");

            var castMethod = _castMethodInfo.MakeGenericMethod(messageType);
            var toListMethod = _toListMethodInfo.MakeGenericMethod(messageType);
            var messagesList = Expression.Call(toListMethod, Expression.Call(castMethod, messages));
            var body = Expression.Call(Expression.Convert(handler, handlerType), handleMethod, messagesList);

            var lambda = Expression.Lambda(typeof(Action<object, List<IMessage>>), body, handler, messages);

            return (Action<object, List<IMessage>>)lambda.Compile();
        }

        private static MethodInfo GetHandleMethodOrThrow(Type handlerType, Type messageType)
        {
            var handleMethod = handlerType.GetMethod("Handle", new[] { typeof(List<>).MakeGenericType(messageType) });
            if (handleMethod == null)
                throw new InvalidProgramException(string.Format("The given type {0} is not an IBatchedMessageHandler<{1}>", handlerType.Name, messageType.Name));

            return handleMethod;
        }
    }
}