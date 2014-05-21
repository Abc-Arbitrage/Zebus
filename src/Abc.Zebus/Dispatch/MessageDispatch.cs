using System;
using System.Collections.Generic;
using System.Threading;

namespace Abc.Zebus.Dispatch
{
    public class MessageDispatch
    {
        public readonly bool ShouldRunSynchronously;
        public readonly MessageContext Context;
        public readonly IMessage Message;

        private readonly Action<MessageDispatch, DispatchResult> _continuation;
        private readonly object _exceptionsLock = new object();
        private Dictionary<Type, Exception> _exceptions;
        private int _remainingHandlerCount;

        public Func<IMessageHandlerInvoker, bool> InvokerFilter { get; set; }

        public MessageDispatch(MessageContext context, IMessage message, Action<MessageDispatch, DispatchResult> continuation, bool shouldRunSynchronously = false)
        {
            ShouldRunSynchronously = shouldRunSynchronously;
            Context = context;
            Message = message;
            _continuation = continuation;
        }

        public void SetIgnored()
        {
            _continuation(this, new DispatchResult(false, null));
        }

        public void SetHandled(IMessageHandlerInvoker invoker, Exception error)
        {
            var remainingHandlerCount = Interlocked.Decrement(ref _remainingHandlerCount);
            if (error != null)
                AddException(invoker.MessageHandlerType, error);

            if (remainingHandlerCount == 0)
                _continuation(this, new DispatchResult(true, _exceptions));
        }

        public void SetHandlerCount(int handlerCount)
        {
            _remainingHandlerCount = handlerCount;
        }

        public bool ShouldInvoke(IMessageHandlerInvoker invoker)
        {
            return InvokerFilter == null || InvokerFilter(invoker);
        }

        private void AddException(Type messageHandlerType, Exception exception)
        {
            lock (_exceptionsLock)
            {
                if (_exceptions == null)
                    _exceptions = new Dictionary<Type, Exception>();

                _exceptions[messageHandlerType] = exception;
            }
        }
    }
}