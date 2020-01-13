using Abc.Zebus.Directory.Storage;
using Abc.Zebus.Routing;
using ProtoBuf;
using RocksDbSharp;
using StructureMap;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using log4net;

namespace Abc.Zebus.Directory.RocksDb.Storage
{
    public partial class RocksDbPeerRepository : IPeerRepository, IDisposable
    {
        private const int _peerIdLengthByteCount = 2;
        private RocksDbSharp.RocksDb _db;

        /// <summary>
        /// Key structure:
        /// ----------------------
        /// |  PeerId (n bytes)  |
        /// ----------------------
        /// </summary>
        private ColumnFamilyHandle _peersColumnFamily;

        /// <summary>
        /// Key structure:
        /// -----------------------------------------------------------------------------------
        /// |  PeerId (n bytes)  |  MessageFullTypeName (n bytes)  | PeerId Length (2 bytes)  |
        /// -----------------------------------------------------------------------------------
        /// </summary>
        private ColumnFamilyHandle _subscriptionsColumnFamily;

        [DefaultConstructor]
        public RocksDbPeerRepository()
            : this(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database"))
        {
        }

        internal RocksDbPeerRepository(string databaseDirectoryPath)
        {
            DatabaseFilePath = databaseDirectoryPath;

            Start();
        }

        private void Start()
        {
            var options = new DbOptions().SetCreateIfMissing()
                                         .SetCreateMissingColumnFamilies()
                                         .SetMaxBackgroundCompactions(4)
                                         .SetMaxBackgroundFlushes(2)
                                         .SetBytesPerSync(1024 * 1024);

            var columnFamilies = new ColumnFamilies
            {
                { "Peers", ColumnFamilyOptions() },
                { "Subscriptions", ColumnFamilyOptions() }
            };

            _db = RocksDbSharp.RocksDb.Open(options, DatabaseFilePath, columnFamilies);

            _peersColumnFamily = _db.GetColumnFamily("Peers");
            _subscriptionsColumnFamily = _db.GetColumnFamily("Subscriptions");

            static ColumnFamilyOptions ColumnFamilyOptions() => new ColumnFamilyOptions().SetCompression(CompressionTypeEnum.rocksdb_no_compression)
                                                                                         .SetLevelCompactionDynamicLevelBytes(true)
                                                                                         .SetArenaBlockSize(16 * 1024);
        }

        internal string DatabaseFilePath { get; }

        public void AddDynamicSubscriptionsForTypes(PeerId peerId, DateTime timestampUtc, SubscriptionsForType[] subscriptionsForTypes)
        {
            if (subscriptionsForTypes == null)
                return;

            var peer = Get(peerId);
            if (peer?.TimestampUtc > timestampUtc)
                return;

            foreach (var subscription in subscriptionsForTypes)
            {
                var key = BuildSubscriptionKey(peerId, subscription.MessageTypeId);
                var value = SerializeBindingKeys(subscription.BindingKeys);
                _db.Put(key, value, _subscriptionsColumnFamily);
            }
        }

        public void AddOrUpdatePeer(PeerDescriptor peerDescriptor)
        {
            var key = BuildPeerKey(peerDescriptor.PeerId);
            var peer = peerDescriptor.Peer;
            var bindingKeys = peerDescriptor.Subscriptions.ToArray();
            var storagePeer = new RocksDbStoragePeer(peerDescriptor.PeerId.ToString(),
                                                     peer.EndPoint,
                                                     peer.IsUp,
                                                     peer.IsResponding,
                                                     peerDescriptor.IsPersistent,
                                                     peerDescriptor.TimestampUtc.GetValueOrDefault(),
                                                     peerDescriptor.HasDebuggerAttached,
                                                     bindingKeys);
            var destination = new MemoryStream();
            Serializer.Serialize(destination, storagePeer);
            var value = destination.ToArray();

            _db.Put(key, value, _peersColumnFamily);
        }

        public PeerDescriptor Get(PeerId peerId)
        {
            var peerStorageBytes = _db.Get(BuildPeerKey(peerId), _peersColumnFamily);
            if (peerStorageBytes == null)
                return null;

            return DeserialisePeerDescriptor(peerId, peerStorageBytes, GetDynamicSubscriptions(peerId));
        }

        private static PeerDescriptor DeserialisePeerDescriptor(PeerId peerId, byte[] peerStorageBytes, ICollection<Subscription> dynamicSubscriptions = null)
        {
            var peerStorage = Serializer.Deserialize<RocksDbStoragePeer>(new MemoryStream(peerStorageBytes));
            var staticSubscriptions = peerStorage.StaticSubscriptions ?? Array.Empty<Subscription>();
            var subscriptions = staticSubscriptions.Concat(dynamicSubscriptions ?? Array.Empty<Subscription>()).Distinct();
            return new PeerDescriptor(peerId, peerStorage.EndPoint, peerStorage.IsPersistent, peerStorage.IsUp, peerStorage.IsResponding, peerStorage.TimestampUtc, subscriptions.ToArray())
            {
                HasDebuggerAttached = peerStorage.HasDebuggerAttached,
            };
        }

        private Subscription[] GetDynamicSubscriptions(PeerId peerId)
        {
            var bindingKeys = new List<(MessageTypeId, BindingKey[])>();
            IterateDynamicSubscriptionsForPeer(peerId,
                                               (key, value) =>
                                               {
                                                   var (bindingKey, messageTypeId) = ReadDynamicSubscription(key, value);
                                                   bindingKeys.Add((messageTypeId, bindingKey));
                                               });
            return bindingKeys.SelectMany(y => y.Item2.Select(bindingKey => new Subscription(y.Item1, bindingKey))).ToArray();
        }

        private void IterateDynamicSubscriptionsForPeer(PeerId peerId, Action<byte[], byte[]> action)
        {
            var key = BuildSubscriptionKeyPrefix(peerId);
            using var cursor = _db.NewIterator(_subscriptionsColumnFamily);
            if (!cursor.Seek(key).Valid())
                return;

            var peerIdLength = peerId.ToString().Length;
            byte[] currentKey;
            do
            {
                currentKey = cursor.Key();
                action(currentKey, cursor.Value());
                cursor.Next();
            } while (cursor.Valid() && CompareStart(currentKey, key, peerIdLength));
        }

        private static (BindingKey[] bindingKeys, MessageTypeId messageTypeId) ReadDynamicSubscription(byte[] key, byte[] value)
        {
            var messageTypeId = GetMessageTypeIdFromKey(key);
            var bindingKeys = DeserializeBindingKeys(value);
            return (bindingKeys, messageTypeId);
        }

        private static MessageTypeId GetMessageTypeIdFromKey(byte[] currentKey)
        {
            var length = ReadPeerIdLength(currentKey);
            var peerIdByteCount = length + _peerIdLengthByteCount;
            if (currentKey.Length <= peerIdByteCount)
                throw new ApplicationException("Key does not contain message type id");

            return new MessageTypeId(Encoding.UTF8.GetString(currentKey, length, currentKey.Length - length - _peerIdLengthByteCount));
        }

        public IEnumerable<PeerDescriptor> GetPeers(bool loadDynamicSubscriptions = true)
        {
            var bindingKeys = new Dictionary<PeerId, List<Subscription>>();
            if (loadDynamicSubscriptions)
            {
                using var cursor = _db.NewIterator(_subscriptionsColumnFamily);
                for (cursor.SeekToFirst(); cursor.Valid(); cursor.Next())
                {
                    var key = cursor.Key();
                    var value = cursor.Value();
                    var peerId = GetPeerIdFromSubscriptionKey(key);
                    var (bindingKey, messageTypeId) = ReadDynamicSubscription(key, value);
                    foreach (var subscription in bindingKey.Select(x => new Subscription(messageTypeId, x)))
                    {
                        if (!bindingKeys.TryGetValue(peerId, out var subscriptions))
                        {
                            subscriptions = new List<Subscription>();
                            bindingKeys.Add(peerId, subscriptions);
                        }

                        subscriptions.Add(subscription);
                    }
                }
            }

            using (var cursor = _db.NewIterator(_peersColumnFamily))
            {
                for (cursor.SeekToFirst(); cursor.Valid(); cursor.Next())
                {
                    var key = cursor.Key();
                    var peerId = GetPeerIdFromPeerKey(key);
                    var value = cursor.Value();
                    bindingKeys.TryGetValue(peerId, out var dynamicSubscriptions);
                    yield return DeserialisePeerDescriptor(GetPeerIdFromPeerKey(key), value, dynamicSubscriptions);
                }
            }
        }

        public bool? IsPersistent(PeerId peerId) => Get(peerId)?.IsPersistent;

        public void RemoveAllDynamicSubscriptionsForPeer(PeerId peerId, DateTime timestampUtc) => IterateDynamicSubscriptionsForPeer(peerId, (key, value) => _db.Remove(key, _subscriptionsColumnFamily));

        public void RemoveDynamicSubscriptionsForTypes(PeerId peerId, DateTime timestampUtc, MessageTypeId[] messageTypeIds)
        {
            if (messageTypeIds == null)
                return;

            var peer = Get(peerId);
            if (peer?.TimestampUtc > timestampUtc)
                return;

            IterateDynamicSubscriptionsForPeer(peerId,
                                               (key, value) =>
                                               {
                                                   var messageTypeId = GetMessageTypeIdFromKey(key);
                                                   if (messageTypeIds.Contains(messageTypeId))
                                                       _db.Remove(key, _subscriptionsColumnFamily);
                                               });
        }

        public void RemovePeer(PeerId peerId)
        {
            _db.Remove(BuildPeerKey(peerId), _peersColumnFamily);

            IterateDynamicSubscriptionsForPeer(peerId, (key, value) => _db.Remove(key, _subscriptionsColumnFamily));
        }

        public void SetPeerResponding(PeerId peerId, bool isResponding)
        {
            var peer = Get(peerId);
            peer.Peer.IsResponding = isResponding;
            AddOrUpdatePeer(peer);
        }

        private static byte[] BuildPeerKey(PeerId peerId) => Encoding.UTF8.GetBytes(peerId.ToString());

        private static PeerId GetPeerIdFromPeerKey(byte[] keyBytes) => new PeerId(Encoding.UTF8.GetString(keyBytes));

        private static PeerId GetPeerIdFromSubscriptionKey(byte[] subscriptionKeyBytes) => new PeerId(Encoding.UTF8.GetString(subscriptionKeyBytes, 0, ReadPeerIdLength(subscriptionKeyBytes)));

        private static byte[] BuildSubscriptionKey(PeerId peerId, MessageTypeId messageTypeId)
        {
            var peerPart = Encoding.UTF8.GetBytes(peerId.ToString());
            var messageTypeIdPart = Encoding.UTF8.GetBytes(messageTypeId.FullName);

            var key = new byte[peerPart.Length + messageTypeIdPart.Length + _peerIdLengthByteCount];
            Buffer.BlockCopy(peerPart, 0, key, 0, peerPart.Length);
            Buffer.BlockCopy(messageTypeIdPart, 0, key, peerPart.Length, messageTypeIdPart.Length);
            WritePeerIdLength(key, peerPart.Length);

            return key;
        }

        private static byte[] BuildSubscriptionKeyPrefix(PeerId peerId)
        {
            var peerPart = Encoding.UTF8.GetBytes(peerId.ToString());
            var key = new byte[peerPart.Length];
            Buffer.BlockCopy(peerPart, 0, key, 0, peerPart.Length);
            return key;
        }

        private static short ReadPeerIdLength(byte[] subscriptionKeyBytes) => BitConverter.ToInt16(subscriptionKeyBytes, subscriptionKeyBytes.Length - _peerIdLengthByteCount);

        private static void WritePeerIdLength(byte[] key, int peerPartLength)
        {
            var shortLength = (short)peerPartLength;
            key[key.Length - 2] = (byte)shortLength;
            key[key.Length - 1] = (byte)((uint)shortLength >> 8);
        }

        private static BindingKey[] DeserializeBindingKeys(byte[] bindingKeysBytes)
        {
            using var memoryStream = new MemoryStream(bindingKeysBytes);
            using var binaryReader = new BinaryReader(memoryStream);

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

        private static byte[] SerializeBindingKeys(BindingKey[] bindingKeys)
        {
            using var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);

            binaryWriter.Write(bindingKeys.Length);
            foreach (var bindingKey in bindingKeys)
            {
                binaryWriter.Write(bindingKey.PartCount);

                for (var partIndex = 0; partIndex < bindingKey.PartCount; partIndex++)
                    binaryWriter.Write(bindingKey.GetPart(partIndex));
            }

            return memoryStream.ToArray();
        }

        private static bool CompareStart(byte[] x, byte[] y, int length)
        {
            if (x.Length < length || y.Length < length)
                return false;

            for (var index = length - 1; index >= 0; index--)
            {
                if (x[index] != y[index])
                    return false;
            }

            return true;
        }

        public void Dispose() => _db?.Dispose();
    }
}
