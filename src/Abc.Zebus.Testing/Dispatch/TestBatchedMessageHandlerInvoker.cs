using System;
using System.Collections.Generic;
using Abc.Zebus.Dispatch;
using Moq;
using StructureMap;

namespace Abc.Zebus.Testing.Dispatch
{
    public class TestBatchedMessageHandlerInvoker<TMessage> : BatchedMessageHandlerInvoker where TMessage : class, IEvent
    {
        public TestBatchedMessageHandlerInvoker() : this(new Mock<IContainer>())
        {
        }

        private TestBatchedMessageHandlerInvoker(Mock<IContainer> container)
            : base(container.Object, typeof(Handler), typeof(TMessage))
        {
            container.Setup(x => x.GetInstance(typeof(Handler))).Returns(() => new Handler(HandlerCallback));
        }

        public Action<List<TMessage>> HandlerCallback { get; set; }

        public class Handler : IBatchMessageHandler<TMessage>
        {
            private readonly Action<List<TMessage>> _handlerCallback;

            public Handler(Action<List<TMessage>> handlerCallback)
            {
                _handlerCallback = handlerCallback;
            }

            public void Handle(List<TMessage> messages)
            {
                _handlerCallback?.Invoke(messages);
            }
        }
    }
}