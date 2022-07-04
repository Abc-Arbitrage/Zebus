using Lamar;

namespace Abc.Zebus.DependencyInjection
{
    public class LamarContainerProvider : IDependencyInjectionContainerProvider
    {
        private readonly LamarContainer _lamarContainer;

        public LamarContainerProvider(IContainer container)
        {
            _lamarContainer = new LamarContainer(container);
        }

        public IDependencyInjectionContainer GetContainer()
        {
            return _lamarContainer;
        }
    }
}
