using StructureMap;
using Lamar;

namespace Abc.Zebus.Directory.Initialization
{
    public class LamarDirectoryRegistry : ServiceRegistry
    {
        public LamarDirectoryRegistry()
        {
            ForSingletonOf<IDirectorySpeedReporter>().UseIfNone<NoopDirectorySpeedReporter>();
        }
    }

    public class DirectoryRegistry : Registry
    {
        public DirectoryRegistry()
        {
            ForSingletonOf<IDirectorySpeedReporter>().UseIfNone<NoopDirectorySpeedReporter>();
        }
    }
}
