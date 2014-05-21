using System;

namespace Abc.Zebus.Dispatch
{
    public interface IMessageHandlerInvocation
    {
        IMessage Message { get; }
        MessageContext Context { get; }

        IDisposable SetupForInvocation();
        IDisposable SetupForInvocation(object messageHandler);
    }
}