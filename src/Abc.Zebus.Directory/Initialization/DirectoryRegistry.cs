using StructureMap.Configuration.DSL;

namespace Abc.Zebus.Directory.Initialization
{
    public class DirectoryRegistry : Registry
    {
        public DirectoryRegistry()
        {
            ForSingletonOf<IDirectorySpeedReporter>().UseIfNone<NoopDirectorySpeedReporter>();
        }
    }
}