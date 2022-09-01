using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Scan;

namespace Abc.Zebus.Testing.Dispatch
{
    public class TestAsyncMessageHandlerInvoker<TMessage> : IMessageHandlerInvoker
        where TMessage : class, IMessage
    {
        public bool Invoked { get; private set; }

        public Type MessageHandlerType => typeof(Handler);
        public Type MessageType => typeof(TMessage);
        public MessageTypeId MessageTypeId => new(MessageType);
        public string DispatchQueueName => DispatchQueueNameScanner.DefaultQueueName;

        public MessageHandlerInvokerMode Mode => MessageHandlerInvokerMode.Asynchronous;

        public void InvokeMessageHandler(IMessageHandlerInvocation invocation)
        {
            throw new NotSupportedException();
        }

        public async Task InvokeMessageHandlerAsync(IMessageHandlerInvocation invocation)
        {
            Invoked = true;

            using (invocation.SetupForInvocation())
            {
                foreach (var message in invocation.Messages)
                {
                    (message as IExecutableMessage)?.Execute(invocation);

                    var asyncTask = (message as IAsyncExecutableMessage)?.ExecuteAsync(invocation);
                    if (asyncTask != null)
                        await asyncTask.ConfigureAwait(false);
                }
            }
        }

        public bool ShouldHandle(IMessage message)
        {
            return true;
        }

        public bool CanMergeWith(IMessageHandlerInvoker other)
        {
            return false;
        }

        public IEnumerable<Subscription> GetStartupSubscriptions()
        {
            return new[] { new Subscription(MessageTypeId) };
        }

        private class Handler : IAsyncMessageHandler<TMessage>
        {
            public Task Handle(TMessage message)
            {
                throw new NotSupportedException();
            }
        }
    }
}
