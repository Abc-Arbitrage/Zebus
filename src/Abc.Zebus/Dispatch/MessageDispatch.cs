using System;
using System.Collections.Generic;
using System.Threading;

namespace Abc.Zebus.Dispatch
{
    public class MessageDispatch
    {
        private readonly Dictionary<Type, Exception> _exceptions = new Dictionary<Type, Exception>();
        private readonly Action<MessageDispatch, DispatchResult> _continuation;
        private readonly object _exceptionsLock = new object();
        private int _remainingHandlerCount;

        public MessageDispatch(MessageContext context, IMessage message, Action<MessageDispatch, DispatchResult> continuation, bool shouldRunSynchronously = false)
        {
            _continuation = continuation;

            ShouldRunSynchronously = shouldRunSynchronously;
            Context = context;
            Message = message;
        }

        public bool ShouldRunSynchronously { get; private set; }
        public MessageContext Context { get; private set; }
        public IMessage Message { get; private set; }
        public Func<IMessageHandlerInvoker, bool> InvokerFilter { get; set; }

        public void SetIgnored()
        {
            _continuation(this, new DispatchResult(null));
        }

        public void SetHandled(IMessageHandlerInvoker invoker, Exception error)
        {
            if (error != null)
            {
                lock (_exceptionsLock)
                {
                    _exceptions[invoker.MessageHandlerType] = error;
                }
            }

            if (Interlocked.Decrement(ref _remainingHandlerCount) == 0)
                _continuation(this, new DispatchResult(_exceptions));
        }

        public void SetHandlerCount(int handlerCount)
        {
            _remainingHandlerCount = handlerCount;
        }

        public bool ShouldInvoke(IMessageHandlerInvoker invoker)
        {
            return InvokerFilter == null || InvokerFilter(invoker);
        }
    }
}