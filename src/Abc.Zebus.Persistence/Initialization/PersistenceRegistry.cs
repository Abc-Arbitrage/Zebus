using Abc.Zebus.Persistence.Handlers;
using Abc.Zebus.Persistence.Reporter;
using StructureMap;

namespace Abc.Zebus.Persistence.Initialization
{
    public class PersistenceRegistry : Registry
    {
        public PersistenceRegistry()
        {
            ForSingletonOf<IReporter>().UseIfNone<NoopReporter>();
            ForSingletonOf<PersistMessageCommandHandler>().Use<PersistMessageCommandHandler>();
        }
    }
}
