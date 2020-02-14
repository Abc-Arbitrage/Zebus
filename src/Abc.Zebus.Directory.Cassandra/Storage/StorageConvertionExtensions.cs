using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abc.Zebus.Routing;
using ProtoBuf;

namespace Abc.Zebus.Directory.Cassandra.Storage
{
    public static class StorageConversionExtensions
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

        public static StorageSubscription ToStorageSubscription(this SubscriptionsForType subscriptionFortype, PeerId peerId)
        {
            return new StorageSubscription
            {
                PeerId = peerId.ToString(),
                MessageTypeId = subscriptionFortype.MessageTypeId.FullName,
                SubscriptionBindings = SerializeBindingKeys(subscriptionFortype.BindingKeys)
            };
        }

        public static SubscriptionsForType ToSubscriptionsForType(this StorageSubscription storageSubscription)
        {
            return new SubscriptionsForType(new MessageTypeId(storageSubscription.MessageTypeId), DeserializeBindingKeys(storageSubscription.SubscriptionBindings));
        }

        public static PeerDescriptor ToPeerDescriptor(this StoragePeer storagePeer, IEnumerable<Subscription> peerDynamicSubscriptions)
        {
            if (storagePeer?.StaticSubscriptionsBytes == null)
                return null;

            var staticSubscriptions = DeserializeSubscriptions(storagePeer.StaticSubscriptionsBytes);
            var allSubscriptions = staticSubscriptions.Concat(peerDynamicSubscriptions).Distinct().ToArray();
            return new PeerDescriptor(new PeerId(storagePeer.PeerId), storagePeer.EndPoint, storagePeer.IsPersistent, storagePeer.IsUp,
                                      storagePeer.IsResponding, new DateTime(storagePeer.TimestampUtc.Ticks, DateTimeKind.Utc), allSubscriptions) { HasDebuggerAttached = storagePeer.HasDebuggerAttached };
        }

        public static PeerDescriptor ToPeerDescriptor(this StoragePeer storagePeer)
        {
            if (storagePeer == null)
                return null;
            var staticSubscriptions = DeserializeSubscriptions(storagePeer.StaticSubscriptionsBytes);
            return new PeerDescriptor(new PeerId(storagePeer.PeerId), storagePeer.EndPoint, storagePeer.IsPersistent, storagePeer.IsUp,
                                      storagePeer.IsResponding, new DateTime(storagePeer.TimestampUtc.Ticks, DateTimeKind.Utc), staticSubscriptions) { HasDebuggerAttached = storagePeer.HasDebuggerAttached };
        }

        private static byte[] SerializeSubscriptions(Subscription[] subscriptions)
        {
            using (var stream = new MemoryStream())
            {
                Serializer.Serialize(stream, subscriptions);
                return stream.ToArray();
            }
        }

        private static Subscription[] DeserializeSubscriptions(byte[] subscriptionsBytes)
        {
            return Serializer.Deserialize<Subscription[]>(new MemoryStream(subscriptionsBytes));
        }

        private static byte[] SerializeBindingKeys(BindingKey[] bindingKeys)
        {
            using (var memoryStream = new MemoryStream())
            using (var binaryWriter = new BinaryWriter(memoryStream))
            {
                binaryWriter.Write(bindingKeys.Length);
                for (var keyIndex = 0; keyIndex < bindingKeys.Length; keyIndex++)
                {
                    var bindingKey = bindingKeys[keyIndex];
                    binaryWriter.Write(bindingKey.PartCount);

                    for (var partIndex = 0; partIndex < bindingKey.PartCount; partIndex++)
                        binaryWriter.Write(bindingKey.GetPartToken(partIndex));
                }
                return memoryStream.ToArray();
            }
        }

        private static BindingKey[] DeserializeBindingKeys(byte[] bindingKeysBytes)
        {
            using (var memoryStream = new MemoryStream(bindingKeysBytes))
            using (var binaryReader = new BinaryReader(memoryStream))
            {
                var bindingKeyCount = binaryReader.ReadInt32();
                var bindingKeys = new BindingKey[bindingKeyCount];
                for (var keyIndex = 0; keyIndex < bindingKeyCount; keyIndex++)
                {
                    var partsCount = binaryReader.ReadInt32();
                    var parts = new string[partsCount];

                    for (var partIndex = 0; partIndex < partsCount; partIndex++)
                        parts[partIndex] = binaryReader.ReadString();

                    bindingKeys[keyIndex] = new BindingKey(parts);
                }
                return bindingKeys;
            }
        }
    }
}
