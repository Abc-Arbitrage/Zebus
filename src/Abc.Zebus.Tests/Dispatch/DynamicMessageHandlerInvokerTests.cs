using Abc.Zebus.Dispatch;
using Abc.Zebus.Routing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Dispatch
{
    [TestFixture]
    public class DynamicMessageHandlerInvokerTests
    {
        [TestCase(1, "lal", false)]
        [TestCase(1, "toto", true)]
        [TestCase(1, "titi", true)]
        [TestCase(2, "titi", false)]
        public void should_filter_messages_using_predicate(decimal id, string name, bool shouldBeHandled)
        {
            // Arrange
            var bindingKey1 = new BindingKey("1", "toto", "*");
            var bindingKey2 = new BindingKey("1", "titi", "*");
            var handler = new DynamicMessageHandlerInvoker(_ => { }, typeof(FakeRoutableCommand), new[] { bindingKey1, bindingKey2 });

            var message = new FakeRoutableCommand(id, name);

            // Act
            var handled = handler.ShouldHandle(message);

            // Assert
            handled.ShouldEqual(shouldBeHandled);
        }
    }
}
