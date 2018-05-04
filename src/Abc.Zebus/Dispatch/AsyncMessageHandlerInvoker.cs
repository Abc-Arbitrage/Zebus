using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using StructureMap;

namespace Abc.Zebus.Dispatch
{
    public class AsyncMessageHandlerInvoker : MessageHandlerInvoker
    {
        private readonly IContainer _container;
        private readonly Func<object, IMessage, Task> _handleAction;

        public AsyncMessageHandlerInvoker(IContainer container, Type handlerType, Type messageType, bool shouldBeSubscribedOnStartup = true)
            : base(handlerType, messageType, shouldBeSubscribedOnStartup)
        {
            _container = container;
            _handleAction = GenerateHandleAction(handlerType, messageType);
        }

        public override MessageHandlerInvokerMode Mode => MessageHandlerInvokerMode.Asynchronous;

        internal object CreateHandler(MessageContext messageContext)
        {
            return CreateHandler(_container, messageContext);
        }

        public override void InvokeMessageHandler(IMessageHandlerInvocation invocation)
        {
            throw new NotSupportedException($"{nameof(InvokeMessageHandler)} is not supported in Asynchronous mode");
        }

        public override Task InvokeMessageHandlerAsync(IMessageHandlerInvocation invocation)
        {
            try
            {
                var handler = CreateHandler(_container, invocation.Context);
                using (invocation.SetupForInvocation(handler))
                {
                    return _handleAction(handler, invocation.Messages[0]);
                }
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        private static Func<object, IMessage, Task> GenerateHandleAction(Type handlerType, Type messageType)
        {
            var methodInfo = GetHandleMethodOrThrow(handlerType, messageType);

            var o = Expression.Parameter(typeof(object), "o");
            var m = Expression.Parameter(typeof(IMessage), "m");
            var body = Expression.Call(Expression.Convert(o, handlerType), methodInfo, Expression.Convert(m, messageType));
            var lambda = Expression.Lambda(typeof(Func<object, IMessage, Task>), body, o, m);

            return (Func<object, IMessage, Task>)lambda.Compile();
        }

        private static MethodInfo GetHandleMethodOrThrow(Type handlerType, Type messageType)
        {
            try
            {
                var interfaceType = typeof(IAsyncMessageHandler<>).MakeGenericType(messageType);
                var interfaceMethod = interfaceType.GetMethod(nameof(IAsyncMessageHandler<IMessage>.Handle), new[] { messageType });
                var interfaceMap = handlerType.GetInterfaceMap(interfaceType);
                var handleIndex = Array.IndexOf(interfaceMap.InterfaceMethods, interfaceMethod);
                return interfaceMap.TargetMethods[handleIndex];
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"The given handler type ({handlerType.Name}) is not an {nameof(IAsyncMessageHandler<IMessage>)}<{messageType.Name}>", ex);
            }
        }
    }
}
