using System;
using System.IO;
using System.Linq;
using Abc.Zebus.Routing;
using ProtoBuf;

namespace Abc.Zebus.Directory.Cassandra.Storage
{
    public static class StorageConvertionExtensions
    {
        public static StoragePeer ToStoragePeer(this PeerDescriptor peerDescriptor)
        {
            return new StoragePeer
            {
                PeerId = peerDescriptor.PeerId.ToString(),
                EndPoint = peerDescriptor.Peer.EndPoint,
                HasDebuggerAttached = peerDescriptor.HasDebuggerAttached,
                IsPersistent = peerDescriptor.IsPersistent,
                IsUp = peerDescriptor.Peer.IsUp,
                IsResponding = peerDescriptor.Peer.IsResponding,
                TimestampUtc = peerDescriptor.TimestampUtc ?? DateTime.UtcNow,
                StaticSubscriptionsBytes = SerializeSubscriptions(peerDescriptor.Subscriptions)
            };
        }

        private static byte[] SerializeSubscriptions(Subscription[] subscriptions)
        {
            var stream = new MemoryStream();
            Serializer.Serialize(stream, subscriptions);
            return stream.ToArray();
        }

        public static DynamicSubscription ToStorageSubscription(this Subscription subscription, PeerId peerId)
        {
            return new DynamicSubscription
            {
                PeerId = peerId.ToString(),
                BindingKeyParts = subscription.BindingKey.GetParts().Select((part, i) => new { Index = i, Part = part }).ToDictionary(x => x.Index, x => x.Part),
                MessageTypeFullName = subscription.MessageTypeId.FullName
            };
        }

        public static Subscription ToSubscription(this DynamicSubscription dynamicSubscription)
        {
            var bindingKeyParts = new string[0];
            if (dynamicSubscription.BindingKeyParts != null)
                bindingKeyParts = dynamicSubscription.BindingKeyParts.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToArray();
            
            return new Subscription(new MessageTypeId(dynamicSubscription.MessageTypeFullName), new BindingKey(bindingKeyParts));
        }

        public static PeerDescriptor ToPeerDescriptor(this StoragePeer storagePeer)
        {
            if (storagePeer == null)
                return null;
            return new PeerDescriptor(new PeerId(storagePeer.PeerId), storagePeer.EndPoint, storagePeer.IsPersistent, storagePeer.IsUp,
                                      storagePeer.IsResponding, storagePeer.TimestampUtc, DeserializeSubscriptions(storagePeer.StaticSubscriptionsBytes)) { HasDebuggerAttached = storagePeer.HasDebuggerAttached };
        }

        private static Subscription[] DeserializeSubscriptions(byte[] staticSubscriptionsBytes)
        {
            return Serializer.Deserialize<Subscription[]>(new MemoryStream(staticSubscriptionsBytes));
        }
    }
}