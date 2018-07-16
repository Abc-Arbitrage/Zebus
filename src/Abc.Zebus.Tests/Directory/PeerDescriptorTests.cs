using Abc.Zebus.Directory;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Directory
{
    [TestFixture]
    public class PeerDescriptorTests
    {
        [Test]
        public void should_return_the_corresponding_peer()
        {
            var descriptor = new PeerDescriptor(new PeerId("theID"), "tcp://endpoint:123", true, true, true, SystemDateTime.UtcNow, new[] { new Subscription(new MessageTypeId(typeof(string))) });

            var peer = descriptor.Peer;

            peer.Id.ShouldEqual(descriptor.Peer.Id);
            peer.EndPoint.ShouldEqual(descriptor.Peer.EndPoint);
            peer.IsUp.ShouldEqual(descriptor.Peer.IsUp);
        }
    }
}