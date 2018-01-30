using System;
using System.Configuration;
using System.IO;
using System.Threading;
using Abc.Zebus.Core;
using Abc.Zebus.Directory;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Monitoring;
using Abc.Zebus.Persistence.CQL;
using Abc.Zebus.Persistence.CQL.Storage;
using Abc.Zebus.Persistence.CQL.Util;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Persistence.Reporter;
using Abc.Zebus.Persistence.Storage;
using Abc.Zebus.Persistence.Transport;
using Abc.Zebus.Transport;
using log4net;
using log4net.Config;
using StructureMap;

namespace Abc.Zebus.Persistence.Runner
{
    internal class Program
    {
        private static readonly ManualResetEvent _cancelKeySignal = new ManualResetEvent(false);
        private static readonly ILog _log = LogManager.GetLogger(typeof(Program));

        public static void Main()
        {
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                _cancelKeySignal.Set();
            };

            XmlConfigurator.ConfigureAndWatch(new FileInfo(InBaseDirectory("log4net.config")));
            _log.Info("Starting persistence");

            var busFactory = new BusFactory();
            var appSettingsConfiguration = new AppSettingsConfiguration();
            InjectPersistenceServiceSpecificConfiguration(busFactory, appSettingsConfiguration);

            busFactory
                .WithConfiguration(appSettingsConfiguration, ConfigurationManager.AppSettings["Environment"])
                .WithScan()
                .WithEndpoint(ConfigurationManager.AppSettings["Endpoint"])
                .WithPeerId(ConfigurationManager.AppSettings["PeerId"]);

            using (busFactory.CreateAndStartBus())
            {
                _log.Info("Persistence started");

                _cancelKeySignal.WaitOne();
            }
        }

        private static string InBaseDirectory(string path)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
        }

        private static void InjectPersistenceServiceSpecificConfiguration(BusFactory busFactory, AppSettingsConfiguration configuration)
        {
            busFactory.ConfigureContainer(c =>
            {
                c.ForSingletonOf<IPersistenceConfiguration>().Use(configuration);

                // TODO: Add InMemoryStorage
                c.ForSingletonOf<IStorage>().Use<CqlStorage>();

                c.ForSingletonOf<IMessageReplayerRepository>().Use<MessageReplayerRepository>();
                c.ForSingletonOf<IMessageReplayer>().Use<MessageReplayer>();
                
                c.ForSingletonOf<IMessageDispatcher>().Use(typeof(Func<IContext, MessageDispatcher>).Name,
                                                           ctx =>
                                                           {
                                                               var dispatcher = ctx.GetInstance<MessageDispatcher>();
                                                               dispatcher.ConfigureHandlerFilter(x => x != typeof(PeerDirectoryClient));

                                                               return dispatcher;
                                                           });

                c.ForSingletonOf<ITransport>().Use<QueueingTransport>().Ctor<ITransport>().Is<ZmqTransport>();
                c.ForSingletonOf<IInMemoryMessageMatcher>().Use<InMemoryMessageMatcher>();
                c.Forward<IInMemoryMessageMatcher, IProvideQueueLength>();
                c.ForSingletonOf<IStoppingStrategy>().Use<PersistenceStoppingStrategy>();

                c.ForSingletonOf<IReporter>().Use<NoopReporter>();

                // Cassandra specific
                c.ForSingletonOf<PeerStateRepository>().Use<PeerStateRepository>();
                c.ForSingletonOf<CassandraCqlSessionManager>().Use(() => CassandraCqlSessionManager.Create());
                c.Forward<PeerStateRepository, IPeerStateRepository>();
                c.ForSingletonOf<ICqlPersistenceConfiguration>().Use<CassandraAppSettingsConfiguration>();
            });
        }
    }
}
