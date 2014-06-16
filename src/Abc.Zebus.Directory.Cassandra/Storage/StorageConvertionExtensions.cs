using System;
using System.Collections.Generic;
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
            var timestamp = peerDescriptor.TimestampUtc.HasValue ? new DateTime(peerDescriptor.TimestampUtc.Value.Ticks, DateTimeKind.Utc) : DateTime.UtcNow;
            return new StoragePeer
            {
                PeerId = peerDescriptor.PeerId.ToString(),
                EndPoint = peerDescriptor.Peer.EndPoint,
                HasDebuggerAttached = peerDescriptor.HasDebuggerAttached,
                IsPersistent = peerDescriptor.IsPersistent,
                IsUp = peerDescriptor.Peer.IsUp,
                IsResponding = peerDescriptor.Peer.IsResponding,
                TimestampUtc = timestamp,
                StaticSubscriptionsBytes = SerializeSubscriptions(peerDescriptor.Subscriptions)
            };
        }

        private static byte[] SerializeSubscriptions(Subscription[] subscriptions)
        {
            var stream = new MemoryStream();
            Serializer.Serialize(stream, subscriptions);
            return stream.ToArray();
        }

        public static StorageSubscription ToStorageSubscription(this Subscription subscription, PeerId peerId)
        {
            return new StorageSubscription
            {
                PeerId = peerId.ToString(),
                BindingKeyParts = subscription.BindingKey.GetParts().Select((part, i) => new { Index = i, Part = part }).ToDictionary(x => x.Index, x => x.Part),
                MessageTypeId = subscription.MessageTypeId.FullName
            };
        }

        public static Subscription ToSubscription(this StorageSubscription storageSubscription)
        {
            var bindingKeyParts = new string[0];
            if (storageSubscription.BindingKeyParts != null)
                bindingKeyParts = storageSubscription.BindingKeyParts.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToArray();
            
            return new Subscription(new MessageTypeId(storageSubscription.MessageTypeId), new BindingKey(bindingKeyParts));
        }

        public static PeerDescriptor ToPeerDescriptor(this StoragePeer storagePeer, IEnumerable<Subscription> peerDynamicSubscriptions)
        {
            if (storagePeer == null)
                return null;
            var staticSubscriptions = DeserializeSubscriptions(storagePeer.StaticSubscriptionsBytes);
            var allSubscriptions = staticSubscriptions.Concat(peerDynamicSubscriptions).Distinct().ToArray();
            return new PeerDescriptor(new PeerId(storagePeer.PeerId), storagePeer.EndPoint, storagePeer.IsPersistent, storagePeer.IsUp,
                                      storagePeer.IsResponding, new DateTime(storagePeer.TimestampUtc.Ticks, DateTimeKind.Utc), allSubscriptions) { HasDebuggerAttached = storagePeer.HasDebuggerAttached };
        }

        private static Subscription[] DeserializeSubscriptions(byte[] staticSubscriptionsBytes)
        {
            return Serializer.Deserialize<Subscription[]>(new MemoryStream(staticSubscriptionsBytes));
        }
    }
}