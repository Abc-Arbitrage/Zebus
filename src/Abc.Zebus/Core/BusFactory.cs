using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Initialization;
using Abc.Zebus.Scan;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;
using StructureMap;

namespace Abc.Zebus.Core
{
    public class BusFactory
    {
        private readonly List<Action<ConfigurationExpression>> _configurationActions = new List<Action<ConfigurationExpression>>();
        private readonly ZmqTransportConfiguration _transportConfiguration = new ZmqTransportConfiguration();
        private readonly List<ScanTarget> _scanTargets = new List<ScanTarget>();
        private IBusConfiguration? _configuration;
        private string? _environment;
        public PeerId PeerId { get; set; }

        public BusFactory()
            : this(new Container())
        {
        }

        public BusFactory(IContainer container)
        {
            PeerId = new PeerId("Abc.Testing." + Guid.NewGuid());

            _scanTargets.Add(new ScanTarget(typeof(IBus).Assembly, null));

            Container = container;
        }

        public IContainer Container { get; }

        public BusFactory WithConfiguration(string directoryEndPoints, string environment)
        {
            var endpoints = directoryEndPoints.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            return WithConfiguration(CreateBusConfiguration(endpoints), environment);
        }

        public BusFactory WithConfiguration(IBusConfiguration configuration, string environment)
        {
            _configuration = configuration;
            _environment = environment;
            return this;
        }

        public BusFactory WithPeerId(string peerId)
        {
            PeerId = new PeerId(peerId.Replace("*", Guid.NewGuid().ToString()));
            return this;
        }

        public BusFactory WithHandlers(params Type[] handlers)
        {
            foreach (var handler in handlers)
            {
                _scanTargets.Add(new ScanTarget(handler.Assembly, handler));
            }

            return this;
        }

        public BusFactory WithScan()
        {
            _scanTargets.Add(new ScanTarget(null, null));
            return this;
        }

        public BusFactory WithScan(IEnumerable<Assembly> assemblies)
        {
            foreach (var assembly in assemblies)
            {
                _scanTargets.Add(new ScanTarget(assembly, null));
            }

            return this;
        }

        public BusFactory ConfigureContainer(Action<ConfigurationExpression> containerConfiguration)
        {
            _configurationActions.Add(containerConfiguration);
            return this;
        }

        public IBus CreateAndStartBus()
        {
            var bus = CreateBus();
            bus.Start();

            return bus;
        }

        public IBus CreateBus()
        {
            if (_configuration == null || _environment == null)
                throw new InvalidOperationException("The CreateBus() method was called with no configuration (Call .WithConfiguration(...) first)");

            Container.Configure(x => x.AddRegistry<ZebusRegistry>());
            Container.Configure(x =>
            {
                x.ForSingletonOf<IBusConfiguration>().Use(_configuration);
                x.ForSingletonOf<IZmqTransportConfiguration>().Use(_transportConfiguration);
                x.ForSingletonOf<IMessageDispatcher>().Use(
                    "MessageDispatcher factory",
                    ctx =>
                    {
                        var dispatcher = new MessageDispatcher(ctx.GetAllInstances<IMessageHandlerInvokerLoader>().ToArray(), ctx.GetInstance<IDispatchQueueFactory>());
                        dispatcher.ConfigureHandlerFilter(assembly => _scanTargets.Any(scanTarget => scanTarget.Matches(assembly)));
                        dispatcher.ConfigureAssemblyFilter(type => _scanTargets.Any(scanTarget => scanTarget.Matches(type)));

                        return dispatcher;
                    }
                );
            });

            foreach (var configurationAction in _configurationActions)
            {
                Container.Configure(configurationAction);
            }

            var bus = Container.GetInstance<IBus>();
            bus.Configure(PeerId, _environment);
            return bus;
        }

        private static IBusConfiguration CreateBusConfiguration(string[] directoryServiceEndPoints)
        {
            return new BusConfiguration(directoryServiceEndPoints)
            {
                IsPersistent = false,
                RegistrationTimeout = 10.Seconds(),
                StartReplayTimeout = 30.Seconds(),
                IsDirectoryPickedRandomly = false,
                IsErrorPublicationEnabled = false,
            };
        }

        public BusFactory WithEndpoint(string endpoint)
        {
            _transportConfiguration.InboundEndPoint = endpoint;
            return this;
        }

        public BusFactory WithWaitForEndOfStreamAckTimeout(TimeSpan timeout)
        {
            _transportConfiguration.WaitForEndOfStreamAckTimeout = timeout;
            return this;
        }

        private class ScanTarget
        {
            private readonly Assembly? _assembly;
            private readonly Type? _type;

            public ScanTarget(Assembly? assembly, Type? type)
            {
                _assembly = assembly;
                _type = type;
            }

            public bool Matches(Assembly assembly)
            {
                return (_assembly == null || _assembly == assembly);
            }

            public bool Matches(Type type)
            {
                if (_assembly == null)
                    return true;

                return type.Assembly == _assembly && (_type == null || _type == type);
            }
        }
    }
}
