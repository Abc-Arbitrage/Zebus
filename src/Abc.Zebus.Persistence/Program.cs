using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Threading;
using Abc.Zebus.Core;
using Abc.Zebus.Directory;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Monitoring;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Persistence.Reporter;
using Abc.Zebus.Persistence.Transport;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using log4net;
using log4net.Config;
using StructureMap;

namespace Abc.Zebus.Persistence
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

            XmlConfigurator.ConfigureAndWatch(new FileInfo(PathUtil.InBaseDirectory("log4net.config")));
            _log.Info("Starting in memory persistence");

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
                _log.Info("In memory persistence started");

                _cancelKeySignal.WaitOne();
            }
        }

        private static void InjectPersistenceServiceSpecificConfiguration(BusFactory busFactory, AppSettingsConfiguration configuration)
        {
            busFactory.ConfigureContainer(c =>
            {
                c.ForSingletonOf<IPersistenceConfiguration>().Use(configuration);

                // TODO: Add InMemoryStorage
//                c.ForSingletonOf<IStorage>().Use<InMemoryStorage>();

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
            });
        }
    }

    internal class AppSettingsConfiguration : IBusConfiguration, IPersistenceConfiguration
    {
        public AppSettingsConfiguration()
        {
            DirectoryServiceEndPoints = AppSettings.GetArray("Bus.Directory.EndPoints");
            RegistrationTimeout = AppSettings.Get("Bus.Directory.RegistrationTimeout", 30.Seconds());
            StartReplayTimeout = AppSettings.Get("Bus.Persistence.StartReplayTimeout", 30.Seconds());
            IsDirectoryPickedRandomly = AppSettings.Get("Bus.Directory.PickRandom", true);
            MessagesBatchSize = AppSettings.Get("Bus.Persistence.MessagesBatchSize", 200);

            PersisterBatchSize = AppSettings.Get("Persister.BatchSize", 500);
            PersisterDelay = AppSettings.Get("Persister.Delay", 30.Seconds());
            SafetyPhaseDuration = AppSettings.Get("Replayer.SafetyPhaseDuration", 30.Seconds());
            QueuingTransportStopTimeout = AppSettings.Get("Transport.StopTimeout", 15.Seconds());
            PeerIdsToInvestigate = AppSettings.GetArray("PeerIdsToInvestigate");
            ReplayBatchSize = AppSettings.Get("MessageReplayer.BatchSize", 2000);
            ReplayUnackedMessageCountThatReleasesNextBatch = AppSettings.Get("MessageReplayer.ReplayUnackedMessageCountThatReleasesNextBatch", 200);
        }

        public string[] DirectoryServiceEndPoints { get; }
        public bool IsPersistent => false;
        public TimeSpan RegistrationTimeout { get; }
        public TimeSpan StartReplayTimeout { get; }
        public bool IsDirectoryPickedRandomly { get; }
        public bool IsErrorPublicationEnabled => false;
        public int MessagesBatchSize { get; }

        public int PersisterBatchSize { get; }
        public TimeSpan? PersisterDelay { get; }
        public TimeSpan SafetyPhaseDuration { get; }
        public TimeSpan QueuingTransportStopTimeout { get; }
        public string[] PeerIdsToInvestigate { get; }
        public int ReplayBatchSize { get; }
        public int ReplayUnackedMessageCountThatReleasesNextBatch { get; }
    }

    internal static class AppSettings
    {
        public static T Get<T>(string key, T defaultValue)
        {
            var value = ConfigurationManager.AppSettings[key];
            if (value == null)
                return defaultValue;

            return Parser<T>.Parse(value);
        }

        public static string[] GetArray(string key)
        {
            var value = ConfigurationManager.AppSettings[key];
            if (value == null)
                return new string[0];

            return value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static class Parser<T>
        {
            private static readonly Func<string, object> _value;

            public static T Parse(string s)
            {
                return (T)_value(s);
            }

            static Parser()
            {
                if (typeof(T) == typeof(TimeSpan))
                    _value = s => TimeSpan.Parse(s, CultureInfo.InvariantCulture);
                else
                    _value = s => Convert.ChangeType(s, typeof(T));
            }
        }
    }
}
