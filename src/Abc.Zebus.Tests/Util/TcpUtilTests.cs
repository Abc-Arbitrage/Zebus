using System.Net;
using System.Net.Sockets;
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Util
{
    [TestFixture]
    public class TcpUtilTests
    {
        [Test]
        public void is_port_unused_should_return_false_if_port_is_used ()
        {
            const int port = 4848;
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            var isPortUnused = TcpUtil.IsPortUnused(port);
            listener.Stop();

            Assert.IsFalse(isPortUnused);
        }

        [Test]
        public void is_port_unused_should_return_true_on_get_random_unused_port ()
        {
            var port = TcpUtil.GetRandomUnusedPort();
            var isPortUnused = TcpUtil.IsPortUnused(port);

            Assert.IsTrue(isPortUnused);
        }
    }
}