using System;

namespace Abc.Zebus.Dispatch
{
    internal class MultiEventHandlerInvoker : MessageHandlerInvoker
    {
        private readonly IMultiEventHandler _handler;
        private readonly Action<object, IMessage> _handleAction;

        public MultiEventHandlerInvoker(Type messageType, IMultiEventHandler handler)
            : base(handler.GetType(), messageType)
        {
            _handler = handler;
            _handleAction = GenerateHandleAction(handler);
        }

        public override void InvokeMessageHandler(IMessageHandlerInvocation invocation)
        {
            using (invocation.SetupForInvocation(_handler))
            {
                _handleAction(_handler, invocation.Message);
            }
        }

        private static Action<object, IMessage> GenerateHandleAction(IMultiEventHandler handler)
        {
            return (_, message) => handler.Handle((IEvent)message);
        }
    }
}