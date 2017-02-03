using System;
using System.Linq.Expressions;
using System.Reflection;
using StructureMap;

namespace Abc.Zebus.Dispatch
{
    public class SyncMessageHandlerInvoker : MessageHandlerInvoker
    {
        private readonly IContainer _container;
        private readonly Action<object, IMessage> _handleAction;

        public SyncMessageHandlerInvoker(IContainer container, Type handlerType, Type messageType, bool shouldBeSubscribedOnStartup = true)
            : this(container, handlerType, messageType, shouldBeSubscribedOnStartup, GenerateHandleAction(handlerType, messageType))
        {
        }

        protected SyncMessageHandlerInvoker(IContainer container, Type handlerType, Type messageType, bool shouldBeSubscribedOnStartup, Action<object, IMessage> handleAction)
            : base(handlerType, messageType, shouldBeSubscribedOnStartup)
        {
            _container = container;
            _handleAction = handleAction;
        }

        public override void InvokeMessageHandler(IMessageHandlerInvocation invocation)
        {
            var handler = CreateHandler(invocation.Context);
            using (invocation.SetupForInvocation(handler))
            {
                _handleAction(handler, invocation.Messages[0]);
            }
        }

        internal object CreateHandler(MessageContext messageContext)
        {
            return CreateHandler(_container, messageContext);
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
            var handleMethod = handlerType.GetMethod(nameof(IMessageHandler<IMessage>.Handle), new[] { messageType });
            if (handleMethod == null)
                throw new InvalidProgramException($"The given type {handlerType.Name} is not an {nameof(IMessageHandler<IEvent>)}<{messageType.Name}>");

            return handleMethod;
        }
    }
}
