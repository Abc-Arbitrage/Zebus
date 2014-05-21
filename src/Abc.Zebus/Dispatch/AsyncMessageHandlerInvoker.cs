using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Abc.Zebus.Util;
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

        public override bool CanInvokeSynchronously
        {
            get { return false; }
        }

        public override bool ShouldCreateStartedTasks
        {
            get { return true; }
        }

        public override void InvokeMessageHandler(IMessageHandlerInvocation invocation)
        {
            throw new NotSupportedException();
        }

        public override Task InvokeMessageHandlerAsync(IMessageHandlerInvocation invocation)
        {
            try
            {
                var handler = CreateHandler(_container, invocation.Context);
                using (invocation.SetupForInvocation(handler))
                {
                    return _handleAction(handler, invocation.Message);
                }
            }
            catch (Exception ex)
            {
                return TaskUtil.FromError(ex);
            }
        }

        private static Func<object, IMessage, Task> GenerateHandleAction(Type handlerType, Type messageType)
        {
            var methodInfo = handlerType.GetMethod("Handle", new[] { messageType });

            var o = Expression.Parameter(typeof(object), "o");
            var m = Expression.Parameter(typeof(IMessage), "m");
            var body = Expression.Call(Expression.Convert(o, handlerType), methodInfo, Expression.Convert(m, messageType));
            var lambda = Expression.Lambda(typeof(Func<object, IMessage, Task>), body, o, m);

            return (Func<object, IMessage, Task>)lambda.Compile();
        }
    }
}
