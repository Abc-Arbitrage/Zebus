using System.Linq;
using Abc.Zebus.Hosting;
using Abc.Zebus.Testing.UnitTesting;
using Abc.Zebus.Util.Extensions;
using Moq;
using NUnit.Framework;
using StructureMap;

namespace Abc.Zebus.Tests.Hosting
{
    public class HostInitializerHelperTests
    {
        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void should_invoke_initializers_in_order(bool invertOrder)
        {
            var container = new Container();
            var initializers = Enumerable.Range(0, 10).Select(CreateMockInitializer).Reverse().ToArray();
            AddInitializersToContainer(container, initializers.Select(x => x.Object).ToArray());
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
            container.Configure(x =>
            {
                foreach (var hostInitializer in initializers)
                    x.For<HostInitializer>().Add(hostInitializer);
            });
        }
    }
}