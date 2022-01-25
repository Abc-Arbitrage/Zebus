using Abc.Zebus.Transport;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Transport
{
    [TestFixture]
    public class OriginatorInfoTests
    {
        [Test]
        [TestCase("tcp://foo.bar.baz:42", ExpectedResult = "foo.bar.baz")]
        [TestCase("tcp://machine:42", ExpectedResult = "machine")]
        public string should_get_host_name_from_endpoint(string endpoint)
            => new OriginatorInfo(default, endpoint, null, null).GetSenderHostNameFromEndPoint();

        [Test]
        [TestCase("tcp://foo.bar.baz:42", ExpectedResult = "foo")]
        [TestCase("tcp://machine:42", ExpectedResult = "machine")]
        public string should_get_machine_name_from_endpoint(string endpoint)
            => new OriginatorInfo(default, endpoint, null, null).GetSenderMachineNameFromEndPoint();
    }
}
