using System;
using JetBrains.Annotations;
using Lamar;

namespace Abc.Zebus.DependencyInjection
{
    [UsedImplicitly]
    public class LamarContainerProvider : IDependencyInjectionContainerProvider
    {
        private readonly IContainer _lamarContainer;

        public LamarContainerProvider(IContainer container)
        {
            _lamarContainer = container;
        }

        public IDependencyInjectionContainer GetContainer()
        {
            return new LamarContainer(_lamarContainer);
        }

        public IMessageHandlerContainer GetMessageHandlerInstanceProvider(Type handlerType)
        {
            return new LamarMessageHandlerContainer(new LamarContainer(_lamarContainer), handlerType);
        }
    }
}
