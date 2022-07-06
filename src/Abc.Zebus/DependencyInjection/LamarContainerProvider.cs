using System;
using Lamar;

namespace Abc.Zebus.DependencyInjection
{
    public class LamarContainerProvider : IDependencyInjectionContainerProvider
    {
        private readonly IContainer _lamarContainer;

        public LamarContainerProvider(IContainer container)
        {
            _lamarContainer = container;
        }

        public IDependencyInjectionContainer GetContainer(Type handlerType)
        {
            return new LamarContainer(_lamarContainer, handlerType);
        }
    }
}
