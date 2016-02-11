using Abc.Zebus.Testing.Comparison;
using Abc.Zebus.Testing.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Comparison
{
    [TestFixture]
    public class ComparisonExtensionsTests
    {
        [Test]
        public void should_ignore_static_members_in_comparison()
        {
            var comparer = ComparisonExtensions.CreateComparer();
            var foo1 = new Foo();
            var foo2 = new Foo();

            var result = comparer.Compare(foo1, foo2);

            result.AreEqual.ShouldBeTrue();
        }

        public class Foo
        {
            private static int _state;

            public static int Value => _state++;
        }
    }
}