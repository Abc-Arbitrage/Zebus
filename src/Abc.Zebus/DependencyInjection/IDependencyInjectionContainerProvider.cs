using System;

namespace Abc.Zebus.DependencyInjection
{
    public interface IDependencyInjectionContainerProvider
    {
        public IDependencyInjectionContainer GetContainer();
        public IMessageHandlerContainer GetMessageHandlerInstanceProvider(Type handlerType);
    }
}
