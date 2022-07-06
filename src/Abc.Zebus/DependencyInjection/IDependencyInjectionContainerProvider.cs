using System;

namespace Abc.Zebus.DependencyInjection
{
    public interface IDependencyInjectionContainerProvider
    {
        public IDependencyInjectionContainer GetContainer(Type handlerType);
    }
}
