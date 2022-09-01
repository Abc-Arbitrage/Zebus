using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Scan;
using Abc.Zebus.Testing.Extensions;

namespace Abc.Zebus.Testing.Dispatch
{
    public class TestMessageHandlerInvoker<TMessage> : IMessageHandlerInvoker where TMessage : class, IMessage
    {
        private readonly bool _shouldBeSubscribedOnStartup;

        public TestMessageHandlerInvoker(bool shouldBeSubscribedOnStartup = true)
        {
            _shouldBeSubscribedOnStartup = shouldBeSubscribedOnStartup;
        }

        public bool Invoked { get; private set; }

        public Type MessageHandlerType => typeof(Handler);
        public Type MessageType => typeof(TMessage);
        public MessageTypeId MessageTypeId => new(MessageType);
        public string DispatchQueueName => DispatchQueueNameScanner.DefaultQueueName;
        public MessageHandlerInvokerMode Mode => MessageHandlerInvokerMode.Synchronous;

        public IEnumerable<Subscription> GetStartupSubscriptions()
        {
            return _shouldBeSubscribedOnStartup
                ? new[] { new Subscription(MessageTypeId) }
                : Array.Empty<Subscription>();
        }

        public void InvokeMessageHandler(IMessageHandlerInvocation invocation)
        {
            Invoked = true;

            using (invocation.SetupForInvocation())
            {
                var message = invocation.Messages.ExpectedSingle() as IExecutableMessage;
                message?.Execute(invocation);
            }
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

        public class Handler : IMessageHandler<TMessage>
        {
            public void Handle(TMessage message)
            {
                throw new NotSupportedException();
            }
        }
    }
}
