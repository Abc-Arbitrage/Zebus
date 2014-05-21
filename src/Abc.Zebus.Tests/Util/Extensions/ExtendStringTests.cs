using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Util.Extensions
{
    [TestFixture]
    public class ExtendStringTests
    {
        [Test]
        [TestCase("a.b.c", "a.b")]
        [TestCase(null, null)]
        [TestCase("no_delimiter", "no_delimiter")]
        [TestCase("a.", "a")]
        public void should_extract_qualifier(string inputString, string expectedQualifier)
        {
            inputString.Qualifier().ShouldEqual(expectedQualifier);
        }
    }
}