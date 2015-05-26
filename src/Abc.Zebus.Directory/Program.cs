using System;
using System.Configuration;
using System.IO;
using System.Threading;
using Abc.Zebus.Core;
using Abc.Zebus.Directory.Configuration;
using Abc.Zebus.Directory.DeadPeerDetection;
using Abc.Zebus.Directory.Storage;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Util;
using log4net;
using log4net.Config;
using StructureMap;

namespace Abc.Zebus.Directory
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
            _log.Info("Starting in memory directory");

            var busFactory = new BusFactory();
            InjectDirectoryServiceSpecificConfiguration(busFactory);

            busFactory
                .WithConfiguration(new AppSettingsBusConfiguration(), ConfigurationManager.AppSettings["Environment"])
                .WithScan()
                .WithEndpoint(ConfigurationManager.AppSettings["Endpoint"])
                .WithPeerId(ConfigurationManager.AppSettings["PeerId"]);

            using (busFactory.CreateAndStartBus())
            {
                _log.Info("In memory directory started");

                _log.Info("Starting dead peer detector");
                var deadPeerDetector = busFactory.Container.GetInstance<IDeadPeerDetector>();
                deadPeerDetector.Start();

                _cancelKeySignal.WaitOne();

                _log.Info("Stopping dead peer detector");
                deadPeerDetector.Stop();
            }
        }

        private static void InjectDirectoryServiceSpecificConfiguration(BusFactory busFactory)
        {
            busFactory.ConfigureContainer(c =>
            {
                c.ForSingletonOf<IDirectoryConfiguration>().Use<AppSettingsDirectoryConfiguration>();

                c.For<IDeadPeerDetector>().Use<DeadPeerDetector>();
                c.ForSingletonOf<IPeerRepository>().Use<MemoryPeerRepository>();
                c.ForSingletonOf<PeerDirectoryServer>().Use<PeerDirectoryServer>();
                c.ForSingletonOf<IPeerDirectory>().Use(ctx => ctx.GetInstance<PeerDirectoryServer>());

                c.ForSingletonOf<IMessageDispatcher>().Use(typeof(Func<IContext, MessageDispatcher>).Name, ctx =>
                {
                    var dispatcher = ctx.GetInstance<MessageDispatcher>();
                    dispatcher.ConfigureHandlerFilter(x => x != typeof(PeerDirectoryClient));

                    return dispatcher;
                });
            });
        }
    }
}