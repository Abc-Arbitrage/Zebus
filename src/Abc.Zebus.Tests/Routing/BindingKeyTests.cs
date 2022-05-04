using System;
using Abc.Zebus.Routing;
using Abc.Zebus.Testing.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Routing
{
    [TestFixture]
    public class BindingKeyTests
    {
        [Test]
        public void should_use_special_char_for_empty_binding_key()
        {
            var empty = new BindingKey(Array.Empty<string>());

            empty.ToString().ShouldEqual("#");
        }
    }
}
