using System;
using Abc.Zebus.Core;

namespace Abc.Zebus.DependencyInjection
{
    public interface IDependencyInjectionContainer
    {
        object GetMessageHandlerInstance(Type type, MessageContextAwareBus dispatchBus, MessageContext messageContext);
        object GetInstance(Type type);
        T GetInstance<T>();
        bool IsSingleton(Type type);
    }
}
