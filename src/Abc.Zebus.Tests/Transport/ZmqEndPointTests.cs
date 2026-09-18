using System;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Transport;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Transport;

[TestFixture]
public class ZmqEndPointTests
{
    [Test]
    public void should_parse_wildcard_port()
    {
        var (host, port) = ZmqEndPoint.Parse("tcp://*:*");

        port.ShouldBeOfType<ZmqPortSpec.Wildcard>();
        port.ToString().ShouldEqual("*");
    }

    [Test]
    public void should_parse_single_port()
    {
        var (host, port) = ZmqEndPoint.Parse("tcp://some.test.fqdn:12000");

        var single = port.ShouldBe<ZmqPortSpec.Single>();
        single.Value.ShouldEqual((ushort)12000);
        port.ToString().ShouldEqual("12000");
    }

    [Test]
    public void should_parse_inclusive_port_range()
    {
        var (host, port) = ZmqEndPoint.Parse("tcp://some.test.fqdn:[12000..12100]/");

        var range = port.ShouldBe<ZmqPortSpec.Range>();
        host.ShouldEqual("some.test.fqdn");
        range.Start.ShouldEqual((ushort)12000);
        range.End.ShouldEqual((ushort)12100);
        port.ToString().ShouldEqual("[12000..12100]");
    }

    [TestCase("tcp://*:[12000..]")]
    [TestCase("tcp://*:[..12100]")]
    [TestCase("tcp://*:[12000...12100]")]
    [TestCase("tcp://*:[12100..12000]")]
    [TestCase("tcp://*:12000..12100")]
    [TestCase("tcp://*:[foo..12100]")]
    [TestCase("tcp://*:[12000..65536]")]
    public void should_reject_invalid_port_ranges(string endpoint)
    {
        Assert.That(() => ZmqEndPoint.Parse(endpoint), Throws.TypeOf<InvalidOperationException>());
    }
}
