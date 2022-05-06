using System;
using System.Linq;
using Abc.Zebus.Util;

namespace Abc.Zebus.Directory.Tests
{
    public static class TestData
    {
        public static PeerDescriptor PersistentPeerDescriptor(string endPoint)
        {
            return PersistentPeerDescriptor(endPoint, Array.Empty<Subscription>());
        }

        public static PeerDescriptor PersistentPeerDescriptor(string endPoint, params Type[] types)
        {
            var subscriptions = types.Select(x => new Subscription(new MessageTypeId(x))).ToArray();
            return new PeerDescriptor(new PeerId("Abc.Testing.0"), endPoint, true, true, true, SystemDateTime.UtcNow, subscriptions);
        }

        public static PeerDescriptor PersistentPeerDescriptor(string endPoint, params Subscription[] subscriptions)
        {
            return new PeerDescriptor(new PeerId("Abc.Testing.0"), endPoint, true, true, true, SystemDateTime.UtcNow, subscriptions);
        }

        public static PeerDescriptor TransientPeerDescriptor(string endPoint, params Type[] types)
        {
            var subscriptions = types.Select(x => new Subscription(new MessageTypeId(x))).ToArray();
            return new PeerDescriptor(new PeerId("Abc.Testing.0"), endPoint, false, true, true, SystemDateTime.UtcNow, subscriptions);
        }
    }
}
