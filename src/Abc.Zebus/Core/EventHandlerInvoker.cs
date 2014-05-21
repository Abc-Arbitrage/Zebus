using System;
using Abc.Zebus.Dispatch;

namespace Abc.Zebus.Core
{
    public class EventHandlerInvoker<T> : MessageHandlerInvoker where T : class, IMessage
    {
        private readonly Action<T> _handler;

        public EventHandlerInvoker(Action<T> handler)
            : base(typeof(DummyHandler), typeof(T), false)
        {
            _handler = handler;
        }

        class DummyHandler : IMessageHandler<T>
        {
            public void Handle(T message)
            {
                throw new NotImplementedException("This handler is only used to provide the base class with a valid implementation of IMessageHandler and is never actually used");
            }
        }

        public override void InvokeMessageHandler(IMessageHandlerInvocation invocation)
        {
            using (invocation.SetupForInvocation())
            {
                _handler((T)invocation.Message);
            }
        }
    }
}