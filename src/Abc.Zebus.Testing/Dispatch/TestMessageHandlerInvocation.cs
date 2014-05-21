using System;
using Abc.Zebus.Dispatch;

namespace Abc.Zebus.Testing.Dispatch
{
    public class TestMessageHandlerInvocation : IMessageHandlerInvocation
    {
        public TestMessageHandlerInvocation(IMessage message, MessageContext context)
        {
            Message = message;
            Context = context;
        }

        public IMessage Message { get; private set; }
        public MessageContext Context { get; private set; }
        public bool ApplyContextCalled { get; private set; }

        public IDisposable SetupForInvocation()
        {
            return SetupForInvocation(null);
        }

        public IDisposable SetupForInvocation(object messageHandler)
        {
            ApplyContextCalled = true;
            return MessageContext.SetCurrent(Context);
        }
    }
}