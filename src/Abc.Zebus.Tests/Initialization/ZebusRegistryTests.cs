using Abc.Zebus.Initialization;
using Abc.Zebus.Transport;
using Moq;
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
            var busConfigurationMock = new Mock<IBusConfiguration>();
            busConfigurationMock.SetupAllProperties();

            var transportConfigurationMock = new Mock<IZmqTransportConfiguration>();
            transportConfigurationMock.SetupAllProperties();

            var container = new Container(cfg =>
            {
                cfg.For<IBusConfiguration>().Use(busConfigurationMock.Object);
                cfg.For<IZmqTransportConfiguration>().Use(transportConfigurationMock.Object);
                cfg.AddRegistry<ZebusRegistry>();
            });

            container.AssertConfigurationIsValid();
        }
    }
}