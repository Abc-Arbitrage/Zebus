using System;
using System.Configuration;
using System.IO;
using System.Threading;
using Abc.Zebus.Core;
using Abc.Zebus.Directory.Cassandra.Cql;
using Abc.Zebus.Directory.Cassandra.Storage;
using Abc.Zebus.Directory.Configuration;
using Abc.Zebus.Directory.DeadPeerDetection;
using Abc.Zebus.Directory.Initialization;
using Abc.Zebus.Directory.RocksDb.Storage;
using Abc.Zebus.Directory.Storage;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Log4Net;
using Abc.Zebus.Util;
using log4net;
using log4net.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StructureMap;

namespace Abc.Zebus.Directory.Runner
{
    internal class Program
    {
        enum StorageType
        {
            Cassandra,
            InMemory,
            RocksDb,
        }

        private static readonly ManualResetEvent _cancelKeySignal = new ManualResetEvent(false);

        private static readonly ILogger _log = ZebusLogManager.GetLogger(typeof(Program));

        public static void Main()
        {
            ZebusLogManager.LoggerFactory = new Log4NetFactory();

            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                _cancelKeySignal.Set();
            };

            XmlConfigurator.ConfigureAndWatch(LogManager.GetRepository(typeof(Program).Assembly), new FileInfo(PathUtil.InBaseDirectory("log4net.config")));
            var storageType = ConfigurationManager.AppSettings["Storage"]!;
            _log.LogInformation($"Starting in directory with storage type '{storageType}'");

            var busFactory = new BusFactory();
            InjectDirectoryServiceSpecificConfiguration(busFactory, Enum.Parse<StorageType>(storageType));

            busFactory
                .WithConfiguration(new AppSettingsBusConfiguration(), ConfigurationManager.AppSettings["Environment"]!)
                .WithScan()
                .WithEndpoint(ConfigurationManager.AppSettings["Endpoint"]!)
                .WithPeerId(ConfigurationManager.AppSettings["PeerId"]!);

            using (busFactory.CreateAndStartBus())
            {
                _log.LogInformation("Directory started");

                _log.LogInformation("Starting dead peer detector");
                var deadPeerDetector = busFactory.Container.GetInstance<IDeadPeerDetector>();
                deadPeerDetector.Start();

                _cancelKeySignal.WaitOne();

                _log.LogInformation("Stopping dead peer detector");
                deadPeerDetector.Stop();
            }
        }

        private static void InjectDirectoryServiceSpecificConfiguration(BusFactory busFactory, StorageType storageType)
        {
            busFactory.Container.Configure(new LamarDirectoryRegistry());
            busFactory.ConfigureContainer(c =>
            {
                c.AddSingleton<IDirectoryConfiguration, AppSettingsDirectoryConfiguration>();

                c.AddTransient<IDeadPeerDetector, DeadPeerDetector>();
                c.AddSingleton<IPeerRepository>(ctx => GetPeerRepository(storageType, ctx)!);
                c.AddSingleton<PeerDirectoryServer>();
                c.AddSingleton<IPeerDirectory>(ctx => ctx.GetService<PeerDirectoryServer>()!);

                c.AddSingleton<IMessageDispatcher>(ctx =>
                {
                    var dispatcher = ctx.GetService<MessageDispatcher>()!;
                    dispatcher.ConfigureHandlerFilter(x => x != typeof(PeerDirectoryClient));

                    return dispatcher;
                });

                // Cassandra specific
                if (storageType == StorageType.Cassandra)
                {
                    c.AddSingleton<CassandraCqlSessionManager>();
                    c.AddSingleton<ICassandraConfiguration, CassandraAppSettingsConfiguration>();
                }
            });
        }

        private static IPeerRepository? GetPeerRepository(StorageType storageType, IServiceProvider ctx)
        {
            return storageType switch
            {
                StorageType.Cassandra => ctx.GetService<CqlPeerRepository>(),
                StorageType.InMemory  => ctx.GetService<MemoryPeerRepository>(),
                StorageType.RocksDb   => ctx.GetService<RocksDbPeerRepository>(),
                _                     => throw new ArgumentOutOfRangeException(nameof(storageType), storageType, null)
            };
        }
    }
}
