using StructureMap;

namespace Abc.Zebus.DependencyInjection
{
    public class  StructureMapContainerProvider : IDependencyInjectionContainerProvider
    {
        private readonly IContainer _structureMapContainer;

        public StructureMapContainerProvider(IContainer structureMapContainer)
        {
            _structureMapContainer = structureMapContainer;
        }

        public IDependencyInjectionContainer GetContainer()
        {
            return new StructureMapContainer(_structureMapContainer);
        }
    }
}
