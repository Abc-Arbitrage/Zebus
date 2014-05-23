using System;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Threading;
using Abc.Zebus.Core;
using Abc.Zebus.Directory.Configuration;
using Abc.Zebus.Directory.Storage;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Util;
using log4net;
using log4net.Config;

namespace Abc.Zebus.Directory
{
    class Program
    {
        private static readonly ManualResetEvent _event = new ManualResetEvent(false);
        private static readonly ILog _log = LogManager.GetLogger(typeof(Program));

        static void Main(string[] args)
        {
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                _event.Set();
            };

            XmlConfigurator.ConfigureAndWatch(new FileInfo(PathUtil.InBaseDirectory("log4net.config")));
            _log.InfoFormat("Starting in memory directory (version: {0})", Assembly.GetExecutingAssembly().GetName().Version.ToString(4));

            var busFactory = new BusFactory();
            busFactory.ConfigureContainer(c =>
            {
                c.For<IDirectoryConfiguration>().Use(AppSettingsDirectoryConfiguration.Current);

                c.ForSingletonOf<IPeerRepository>().Use<MemoryPeerRepository>();
                c.ForSingletonOf<IPeerDirectory>().Use<PeerDirectoryServer>();

                c.ForSingletonOf<IMessageDispatcher>().Use(ctx =>
                {
                    var dispatcher = ctx.GetInstance<MessageDispatcher>();
                    dispatcher.ConfigureHandlerFilter(x => x != typeof(PeerDirectoryClient));

                    return dispatcher;
                });
            });

            busFactory
                .WithConfiguration(AppSettingsBusConfiguration.Current, ConfigurationManager.AppSettings["Environment"])
                .WithScan()
                .WithEndpoint(ConfigurationManager.AppSettings["Endpoint"])
                .WithPeerId(ConfigurationManager.AppSettings["PeerId"]);

            using (var bus = busFactory.CreateAndStartBus())
            {
                _log.Info("In memory directory started");

                _event.WaitOne();
            }
        }
    }
}