using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("Abc.Zebus")]
[assembly: ComVisible(false)]
[assembly: Guid("a287fe42-8da0-46cd-9562-9b35274da061")]

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
[assembly: InternalsVisibleTo("Abc.Zebus.Directory")]
[assembly: InternalsVisibleTo("Abc.Zebus.Directory.Tests")]
[assembly: InternalsVisibleTo("Abc.Zebus.Directory.Cassandra.Tests")]
[assembly: InternalsVisibleTo("Abc.Zebus.Testing")]
[assembly: InternalsVisibleTo("Abc.Zebus.Tests")]
[assembly: InternalsVisibleTo("Abc.Zebus.Persistence")]
[assembly: InternalsVisibleTo("Abc.Zebus.Persistence.Tests")]
[assembly: InternalsVisibleTo("Abc.Zebus.Integration")]
[assembly: InternalsVisibleTo("Abc.Zebus.TestTools")]
[assembly: InternalsVisibleTo("Abc.Zebus.PersistenceService.Tests")]
[assembly: InternalsVisibleTo("Abc.Zebus.DirectoryService.Tests")]
[assembly: InternalsVisibleTo("Abc.Zebus.EventSourcing")]
[assembly: InternalsVisibleTo("Abc.Zebus.EventSourcing.Tests")]
[assembly: InternalsVisibleTo("Abc.Zebus.EventSourcing.TestTools")]
[assembly: InternalsVisibleTo("Abc.Zebus.Saga.Tests")]

// TODO: Remove when the legacy bus is gone
[assembly: InternalsVisibleTo("Abc.Zebus.Gateway")]
[assembly: InternalsVisibleTo("ABC.EventSourcing")]
[assembly: InternalsVisibleTo("ABC.EventSourcing.Tests")]
[assembly: InternalsVisibleTo("Abc.ServiceBus.TestTools")]
[assembly: InternalsVisibleTo("Abc.ServiceBus.Monitoring.Tests")]
[assembly: InternalsVisibleTo("Abc.ConfigManagerService.Tests")]