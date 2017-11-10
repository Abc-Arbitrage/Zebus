using Abc.Zebus.Testing;
using Abc.Zebus.Tests.Messages;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Testing
{
    [TestFixture]
    public class TestBusTests
    {
        [Test]
        public void should_notify_explicit_handlers()
        {
            var bus = new TestBus();

            bool received = false;
            bus.Subscribe<FakeEvent>(e => received = true);

            bus.Publish(new FakeEvent(1));

            Assert.That(received);
        }
    }
}
