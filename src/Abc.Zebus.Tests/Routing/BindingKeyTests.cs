using System;
using Abc.Zebus.Routing;
using Abc.Zebus.Testing;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Tests.Messages;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Routing
{
    [TestFixture]
    public class BindingKeyTests
    {
        [Test]
        public void should_send_routing_key_exception()
        {
            var msg = new FakeRoutableCommand(0, null);

            var exception = Assert.Throws<InvalidOperationException>(() => BindingKey.Create(msg));
            exception.Message.ShouldContain(typeof(FakeRoutableCommand).Name);
            exception.Message.ShouldContain("Name");
            exception.Message.ShouldContain("can not be null");
        }
    }
}
