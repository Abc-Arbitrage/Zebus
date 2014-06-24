using System;

namespace Abc.Zebus.Dispatch
{
    public class EventHandlerInvoker : MessageHandlerInvoker
    {
        private readonly Action<IMessage> _handler;

        public EventHandlerInvoker(Action<IMessage> handler, Type messageType)
            : base(typeof(DummyHandler), messageType, false)
        {
            _handler = handler;
        }

        class DummyHandler : IMessageHandler<IMessage>
        {
            public void Handle(IMessage message)
            {
                throw new NotImplementedException("This handler is only used to provide the base class with a valid implementation of IMessageHandler and is never actually used");
            }
        }

        public override void InvokeMessageHandler(IMessageHandlerInvocation invocation)
        {
            using (invocation.SetupForInvocation())
            {
                _handler(invocation.Message);
            }
        }
    }
}