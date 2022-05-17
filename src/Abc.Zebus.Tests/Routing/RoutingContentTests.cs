using System.Linq;
using Abc.Zebus.Routing;
using Abc.Zebus.Testing.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Routing
{
    [TestFixture]
    public class RoutingContentTests
    {
        [Test]
        public void should_enumerate_content()
        {
            // Arrange
            var values = new[] { new RoutingContentValue("A"), new RoutingContentValue("B") };
            var content = new RoutingContent(values);

            // Act
            var fromEnumerable = content.ToArray();
            // ReSharper disable once RedundantEnumerableCastCall
            var fromUntypedEnumerable = content.Cast<RoutingContentValue>().ToArray();

            // Assert
            fromEnumerable.ShouldEqual(values);
            fromUntypedEnumerable.ShouldEqual(values);
        }

        [Test]
        public void should_iterate_content()
        {
            // Arrange
            var values = new[] { new RoutingContentValue("A"), new RoutingContentValue("B") };
            var content = new RoutingContent(values);

            // Act / Assert
            content.PartCount.ShouldEqual(values.Length);

            for (var i = 0; i < values.Length; i++)
            {
                content[i].ShouldEqual(values[i]);
            }
        }
    }
}
