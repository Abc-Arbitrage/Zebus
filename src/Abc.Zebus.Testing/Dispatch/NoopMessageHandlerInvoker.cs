using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Scan;
using Abc.Zebus.Testing.Extensions;

namespace Abc.Zebus.Testing.Dispatch
{
    public class NoopMessageHandlerInvoker<THandler, TMessage> : IMessageHandlerInvoker where TMessage : class, IMessage
    {
        public Type MessageHandlerType => typeof(THandler);
        public Type MessageType => typeof(TMessage);
        public MessageTypeId MessageTypeId => new(MessageType);
        public string DispatchQueueName => DispatchQueueNameScanner.GetQueueName(typeof(THandler));
        public MessageHandlerInvokerMode Mode => MessageHandlerInvokerMode.Synchronous;

        public IEnumerable<Subscription> GetStartupSubscriptions()
        {
            return Enumerable.Empty<Subscription>();
        }

        public void InvokeMessageHandler(IMessageHandlerInvocation invocation)
        {
        }

        public Task InvokeMessageHandlerAsync(IMessageHandlerInvocation invocation)
        {
            throw new NotSupportedException();
        }

        public bool ShouldHandle(IMessage message)
        {
            return true;
        }

        public bool CanMergeWith(IMessageHandlerInvoker other)
        {
            return false;
        }
    }
}
