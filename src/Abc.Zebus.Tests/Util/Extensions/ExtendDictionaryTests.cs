using System.Collections.Concurrent;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Util.Extensions
{
    [TestFixture]
    public class ExtendDictionaryTests
    {
        [Test]
        public void Should_TryRemove_from_ConcurrentDictionary_using_comparison_value()
        {
            var dict = new ConcurrentDictionary<int, int>();
            dict.TryAdd(42, 12);

            dict.TryRemove(42, 5).ShouldBeFalse();
            dict.Count.ShouldEqual(1);

            dict.TryRemove(42, 12).ShouldBeTrue();
            dict.Count.ShouldEqual(0);
        }
    }
}