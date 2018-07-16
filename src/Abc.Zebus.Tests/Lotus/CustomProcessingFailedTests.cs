using Abc.Zebus.Lotus;
using Abc.Zebus.Testing.Extensions;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Lotus
{
    [TestFixture]
    public class CustomProcessingFailedTests
    {
        [Test]
        public void should_serialize_details()
        {
            var details = new { foo = "bar", baz = 42 };
            var message = new CustomProcessingFailed(typeof(CustomProcessingFailedTests).FullName, "Error").WithDetails(details);

            var deserializedDetails = JsonConvert.DeserializeAnonymousType(message.DetailsJson, details);
            deserializedDetails.ShouldEqual(details);
        }
    }
}
