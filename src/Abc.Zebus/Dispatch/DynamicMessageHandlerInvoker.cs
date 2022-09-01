using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Routing;
using Abc.Zebus.Scan;

namespace Abc.Zebus.Dispatch
{
    public class DynamicMessageHandlerInvoker : IMessageHandlerInvoker
    {
        private readonly Action<IMessage> _handler;
        private readonly List<Func<IMessage, bool>> _predicates;

        public DynamicMessageHandlerInvoker(Action<IMessage> handler, Type messageType, ICollection<BindingKey> bindingKeys)
        {
            var messageTypeId = new MessageTypeId(messageType);

            _handler = handler;
            _predicates = bindingKeys.Select(x => BindingKeyUtil.BuildPredicate(messageTypeId, x)).ToList();

            MessageType = messageType;
            MessageTypeId = messageTypeId;
        }

        public Type MessageHandlerType => typeof(DummyHandler);
        public string DispatchQueueName => DispatchQueueNameScanner.DefaultQueueName;
        public MessageHandlerInvokerMode Mode => MessageHandlerInvokerMode.Synchronous;

        public Type MessageType { get; }
        public MessageTypeId MessageTypeId { get; }

        public IEnumerable<Subscription> GetStartupSubscriptions()
        {
            return Enumerable.Empty<Subscription>();
        }

        public void InvokeMessageHandler(IMessageHandlerInvocation invocation)
        {
            using (invocation.SetupForInvocation())
            {
                foreach (var message in invocation.Messages)
                    _handler(message);
            }
        }

        public Task InvokeMessageHandlerAsync(IMessageHandlerInvocation invocation)
        {
            throw new NotSupportedException("InvokeMessageHandlerAsync is not supported in Synchronous mode");
        }

        public bool ShouldHandle(IMessage message)
        {
            foreach (var predicate in _predicates)
            {
                if (predicate(message))
                    return true;
            }

            return false;
        }

        public bool CanMergeWith(IMessageHandlerInvoker other)
        {
            return false;
        }

        private class DummyHandler : IMessageHandler<IMessage>
        {
            public void Handle(IMessage message)
            {
                throw new NotSupportedException("This handler is only used to provide the base class with a valid implementation of IMessageHandler and is never actually used");
            }
        }
    }
}
