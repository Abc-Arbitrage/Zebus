using System;
using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Routing;

namespace Abc.Zebus.Dispatch
{
    public class DynamicMessageHandlerInvoker : MessageHandlerInvoker
    {
        private readonly Action<IMessage> _handler;
        private readonly List<Func<IMessage, bool>> _predicates;

        public DynamicMessageHandlerInvoker(Action<IMessage> handler, Type messageType, ICollection<BindingKey> bindingKeys)
            : base(typeof(DummyHandler), messageType, false)
        {
            _handler = handler;

            var messageTypeId = MessageUtil.GetTypeId(messageType);
            _predicates = bindingKeys.Select(x => BindingKeyUtil.BuildPredicate(messageTypeId, x)).ToList();
        }

        private class DummyHandler : IMessageHandler<IMessage>
        {
            public void Handle(IMessage message)
            {
                throw new NotSupportedException("This handler is only used to provide the base class with a valid implementation of IMessageHandler and is never actually used");
            }
        }

        public override void InvokeMessageHandler(IMessageHandlerInvocation invocation)
        {
            using (invocation.SetupForInvocation())
            {
                foreach (var message in invocation.Messages)
                    _handler(message);
            }
        }

        public override bool ShouldHandle(IMessage message)
        {
            foreach (var predicate in _predicates)
            {
                if (predicate(message))
                    return true;
            }

            return false;
        }
    }
}
