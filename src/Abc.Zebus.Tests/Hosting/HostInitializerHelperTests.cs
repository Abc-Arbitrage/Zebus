using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.DependencyInjection;
using Abc.Zebus.Hosting;
using Abc.Zebus.Testing.UnitTesting;
using Abc.Zebus.Util.Extensions;
using Lamar;
using Moq;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Hosting
{
    public class HostInitializerHelperTests
    {
        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void should_invoke_initializers_in_order(bool invertOrder)
        {
            var lamarContainer = new Container(new ServiceRegistry());
            var container = new LamarContainer(lamarContainer);
            var initializers = Enumerable.Range(0, 10).Select(CreateMockInitializer).Reverse().ToArray();
            AddInitializersToContainer(lamarContainer, initializers.Select(x => x.Object).ToArray());
            var setupSequence = new SetupSequence();
            (invertOrder ? initializers.Reverse() : initializers).ForEach(x => x.Setup(init => init.BeforeStart()).InSequence(setupSequence));

            container.CallActionOnInitializers(init => init.BeforeStart(), invertOrder);

            setupSequence.Verify();
        }

        private static Mock<HostInitializer> CreateMockInitializer(int priority)
        {
            var firstInitializer = new Mock<HostInitializer>();
            firstInitializer.SetupGet(init => init.Priority).Returns(priority);
            return firstInitializer;
        }

        private static void AddInitializersToContainer(Container container, params HostInitializer[] initializers)
        {
            container.Configure(new TestRegistry(initializers));
        }

        private class TestRegistry : ServiceRegistry
        {
            public TestRegistry(IEnumerable<HostInitializer> initializers)
            {
                foreach (var hostInitializer in initializers)
                    For<HostInitializer>().Add(hostInitializer);
            }
        }
    }
}
