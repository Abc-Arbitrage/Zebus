using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abc.Zebus.Directory.Cassandra.Storage;
using Abc.Zebus.Routing;
using ProtoBuf;

namespace Abc.Zebus.Directory.Cassandra.Data
{
    public static class CassandraExtensions
    {
        public static CassandraPeer ToCassandra(this PeerDescriptor peerDescriptor)
        {
            var timestamp = peerDescriptor.TimestampUtc.HasValue ? new DateTime(peerDescriptor.TimestampUtc.Value.Ticks, DateTimeKind.Utc) : DateTime.UtcNow;
            return new CassandraPeer
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

        public static CassandraSubscription ToCassandra(this SubscriptionsForType subscriptionFortype, PeerId peerId)
        {
            return new CassandraSubscription
            {
                PeerId = peerId.ToString(),
                MessageTypeId = subscriptionFortype.MessageTypeId.FullName!,
                SubscriptionBindings = SerializeBindingKeys(subscriptionFortype.BindingKeys)
            };
        }

        public static SubscriptionsForType ToSubscriptionsForType(this CassandraSubscription subscription)
        {
            return new SubscriptionsForType(
                new MessageTypeId(subscription.MessageTypeId),
                DeserializeBindingKeys(subscription.SubscriptionBindings)
            );
        }

        public static PeerDescriptor? ToPeerDescriptor(this CassandraPeer? peer, IEnumerable<Subscription> peerDynamicSubscriptions)
        {
            if (peer?.StaticSubscriptionsBytes == null)
                return null;

            var staticSubscriptions = DeserializeSubscriptions(peer.StaticSubscriptionsBytes);
            var allSubscriptions = staticSubscriptions.Concat(peerDynamicSubscriptions).Distinct().ToArray();
            return new PeerDescriptor(new PeerId(peer.PeerId),
                                      peer.EndPoint,
                                      peer.IsPersistent,
                                      peer.IsUp,
                                      peer.IsResponding,
                                      new DateTime(peer.TimestampUtc.Ticks, DateTimeKind.Utc),
                                      allSubscriptions) { HasDebuggerAttached = peer.HasDebuggerAttached };
        }

        public static PeerDescriptor? ToPeerDescriptor(this CassandraPeer? peer)
        {
            if (peer == null)
                return null;

            var staticSubscriptions = DeserializeSubscriptions(peer.StaticSubscriptionsBytes);
            return new PeerDescriptor(new PeerId(peer.PeerId),
                                      peer.EndPoint,
                                      peer.IsPersistent,
                                      peer.IsUp,
                                      peer.IsResponding,
                                      new DateTime(peer.TimestampUtc.Ticks, DateTimeKind.Utc),
                                      staticSubscriptions) { HasDebuggerAttached = peer.HasDebuggerAttached };
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
                    {
                        var partToken = bindingKey.GetPartToken(partIndex) ?? "";
                        binaryWriter.Write(partToken);
                    }
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
