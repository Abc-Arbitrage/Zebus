using System;
using System.Linq;
using Abc.Zebus.Util;

namespace Abc.Zebus.Directory.Tests
{
    public static class TestDataBuilder
    {
        public static PeerDescriptor CreatePersistentPeerDescriptor(string endPoint)
        {
            return CreatePersistentPeerDescriptor(endPoint, new Subscription[0]);
        }

        public static PeerDescriptor CreatePersistentPeerDescriptor(string endPoint, params Type[] types)
        {
            var subscriptions = types.Select(x => new Subscription(new MessageTypeId(x))).ToArray();
            return new PeerDescriptor(new PeerId("Abc.Testing.0"), endPoint, true, true, true, SystemDateTime.UtcNow, subscriptions);
        }

        public static PeerDescriptor CreatePersistentPeerDescriptor(string endPoint, params Subscription[] subscriptions)
        {
            return new PeerDescriptor(new PeerId("Abc.Testing.0"), endPoint, true, true, true, SystemDateTime.UtcNow, subscriptions);
        }

        public static PeerDescriptor CreateTransientPeerDescriptor(string endPoint, params Type[] types)
        {
            var subscriptions = types.Select(x => new Subscription(new MessageTypeId(x))).ToArray();
            return new PeerDescriptor(new PeerId("Abc.Testing.0"), endPoint, false, true, true, SystemDateTime.UtcNow, subscriptions);
        }
    }
}