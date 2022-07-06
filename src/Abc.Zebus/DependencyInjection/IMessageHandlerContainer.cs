using System;
using Abc.Zebus.Core;

namespace Abc.Zebus.DependencyInjection
{
    public interface IMessageHandlerContainer : IDependencyInjectionContainer
    {
        object GetMessageHandlerInstance(Type type, MessageContextAwareBus dispatchBus, MessageContext messageContext);
    }
}
