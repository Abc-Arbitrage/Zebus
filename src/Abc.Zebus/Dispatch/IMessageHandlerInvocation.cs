using System;
using System.Collections.Generic;

namespace Abc.Zebus.Dispatch
{
    public interface IMessageHandlerInvocation
    {
        List<IMessage> Messages { get; }
        MessageContext Context { get; }

        IDisposable SetupForInvocation();
        IDisposable SetupForInvocation(object messageHandler);
    }
}