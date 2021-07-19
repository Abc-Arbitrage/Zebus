using Abc.Zebus.Core;
using Abc.Zebus.Initialization;
using Abc.Zebus.Transport;
using NUnit.Framework;
using StructureMap;

namespace Abc.Zebus.Tests.Initialization
{
    [TestFixture]
    public class ZebusRegistryTests
    {
        [Test]
        public void should_have_valid_configuration()
        {
            var busConfiguration = new BusConfiguration(new[] { "tcp://zebus-directory:123" });
            var transportConfiguration = new ZmqTransportConfiguration();

            var container = new Container(cfg =>
            {
                cfg.For<IBusConfiguration>().Use(busConfiguration);
                cfg.For<IZmqTransportConfiguration>().Use(transportConfiguration);
                cfg.AddRegistry<ZebusRegistry>();
            });

            container.AssertConfigurationIsValid();
        }
    }
}
