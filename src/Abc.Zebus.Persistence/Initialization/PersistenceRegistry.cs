using Abc.Zebus.Persistence.Reporter;
using StructureMap.Configuration.DSL;

namespace Abc.Zebus.Persistence.Initialization
{
    public class PersistenceRegistry : Registry
    {
        public PersistenceRegistry()
        {
            ForSingletonOf<IReporter>().UseIfNone<NoopReporter>();
        }
    }
}