using Lamar;

namespace Abc.Zebus.DependencyInjection
{
    public class LamarContainerProvider : IDependencyInjectionContainerProvider
    {
        private readonly IContainer _lamarContainer;

        public LamarContainerProvider(IContainer lamarContainer)
        {
            _lamarContainer = lamarContainer;
        }

        public IDependencyInjectionContainer GetContainer()
        {
            return new LamarContainer(_lamarContainer);
        }
    }
}
