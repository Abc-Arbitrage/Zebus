﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Abc.Zebus.Directory.Storage;
using Abc.Zebus.Routing;
using ProtoBuf;
using RocksDbSharp;
using StructureMap;

namespace Abc.Zebus.Directory.RocksDb.Storage
{
    public partial class RocksDbPeerRepository : IPeerRepository, IDisposable
    {
        private const int _peerIdLengthByteCount = 2;
        private static readonly ThreadLocal<MemoryStream> _memoryStream = new ThreadLocal<MemoryStream>(() => new MemoryStream(1024));
        private RocksDbSharp.RocksDb? _db;

        /// <summary>
        /// Key structure:
        /// ----------------------
        /// |  PeerId (n bytes)  |
        /// ----------------------
        /// </summary>
        private ColumnFamilyHandle? _peersColumnFamily;

        /// <summary>
        /// Key structure:
        /// -----------------------------------------------------------------------------------
        /// |  PeerId (n bytes)  |  MessageFullTypeName (n bytes)  | PeerId Length (2 bytes)  |
        /// -----------------------------------------------------------------------------------
        /// </summary>
        private ColumnFamilyHandle? _subscriptionsColumnFamily;

        [DefaultConstructor]
        public RocksDbPeerRepository()
            : this(Path.Combine(AppDomain.CurrentDomain.BaseDirectory!, "database"))
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

            static ColumnFamilyOptions ColumnFamilyOptions() => new ColumnFamilyOptions().SetCompression(Compression.No)
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
                var (value, valueLength) = SerializeBindingKeys(subscription.BindingKeys);
                var db = GetDb();
                db.Put(key, key.Length, value, valueLength, _subscriptionsColumnFamily);
            }
        }

        private RocksDbSharp.RocksDb GetDb() => _db ?? throw new Exception("Peer repository is not started yet and database is null");

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

            var memoryStream = GetScratchMemoryStream();
            Serializer.Serialize(memoryStream, storagePeer);
            var value = memoryStream.GetBuffer();

            GetDb().Put(key, key.Length, value, memoryStream.Position, _peersColumnFamily);
        }

        public PeerDescriptor? Get(PeerId peerId)
        {
            var peerStorageBytes = GetDb().Get(BuildPeerKey(peerId), _peersColumnFamily);
            if (peerStorageBytes == null)
                return null;

            return DeserializePeerDescriptor(peerId, peerStorageBytes, GetDynamicSubscriptions(peerId));
        }

        private static PeerDescriptor DeserializePeerDescriptor(PeerId peerId, byte[] peerStorageBytes, ICollection<Subscription>? dynamicSubscriptions = null)
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
            using var cursor = GetDb().NewIterator(_subscriptionsColumnFamily);
            if (!cursor.Seek(key).Valid())
                return;

            while (cursor.Valid())
            {
                var currentKey = cursor.Key();
                if (!KeyMatchesPeer(currentKey, key))
                    break;

                action(currentKey, cursor.Value());
                cursor.Next();
            }
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
                using var cursor = GetDb().NewIterator(_subscriptionsColumnFamily);
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

            using (var cursor = GetDb().NewIterator(_peersColumnFamily))
            {
                for (cursor.SeekToFirst(); cursor.Valid(); cursor.Next())
                {
                    var key = cursor.Key();
                    var peerId = GetPeerIdFromPeerKey(key);
                    var value = cursor.Value();
                    bindingKeys.TryGetValue(peerId, out var dynamicSubscriptions);
                    yield return DeserializePeerDescriptor(GetPeerIdFromPeerKey(key), value, dynamicSubscriptions);
                }
            }
        }

        public bool? IsPersistent(PeerId peerId) => Get(peerId)?.IsPersistent;

        public void RemoveAllDynamicSubscriptionsForPeer(PeerId peerId, DateTime timestampUtc)
            => RemoveAllDynamicSubscriptionsForPeer(peerId);

        private void RemoveAllDynamicSubscriptionsForPeer(PeerId peerId)
            => IterateDynamicSubscriptionsForPeer(peerId, (key, value) => GetDb().Remove(key, _subscriptionsColumnFamily));

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
                                                       GetDb().Remove(key, _subscriptionsColumnFamily);
                                               });
        }

        public void RemovePeer(PeerId peerId)
        {
            GetDb().Remove(BuildPeerKey(peerId), _peersColumnFamily);

            RemoveAllDynamicSubscriptionsForPeer(peerId);
        }

        public void SetPeerResponding(PeerId peerId, bool isResponding, DateTime timestampUtc)
        {
            var peer = Get(peerId);
            if (peer != null)
            {
                peer.Peer.IsResponding = isResponding;
                peer.TimestampUtc = timestampUtc;
                AddOrUpdatePeer(peer);
            }
        }

        private static byte[] BuildPeerKey(PeerId peerId) => Encoding.UTF8.GetBytes(peerId.ToString());

        private static PeerId GetPeerIdFromPeerKey(byte[] keyBytes) => new PeerId(Encoding.UTF8.GetString(keyBytes));

        private static PeerId GetPeerIdFromSubscriptionKey(byte[] subscriptionKeyBytes) => new PeerId(Encoding.UTF8.GetString(subscriptionKeyBytes, 0, ReadPeerIdLength(subscriptionKeyBytes)));

        private static byte[] BuildSubscriptionKey(PeerId peerId, MessageTypeId messageTypeId)
        {
            var peerPart = Encoding.UTF8.GetBytes(peerId.ToString()!);
            var messageTypeIdPart = messageTypeId.FullName == null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(messageTypeId.FullName);

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

        private static (byte[] Data, long Length) SerializeBindingKeys(BindingKey[] bindingKeys)
        {
            var memoryStream = GetScratchMemoryStream();
            var binaryWriter = new BinaryWriter(memoryStream, Encoding.Default, true);

            binaryWriter.Write(bindingKeys.Length);
            foreach (var bindingKey in bindingKeys)
            {
                binaryWriter.Write(bindingKey.PartCount);

                for (var partIndex = 0; partIndex < bindingKey.PartCount; partIndex++)
                {
                    var partToken = bindingKey.GetPartToken(partIndex) ?? "";
                    binaryWriter.Write(partToken);
                }
            }

            return (memoryStream.GetBuffer(), memoryStream.Position);
        }

        private static MemoryStream GetScratchMemoryStream()
        {
            var scratchMemoryStream = _memoryStream.Value;
            scratchMemoryStream!.SetLength(0);
            return scratchMemoryStream!;
        }

        private static bool KeyMatchesPeer(byte[] key, byte[] peerIdBytes)
        {
            var keyLength = ReadPeerIdLength(key);
            if (keyLength != peerIdBytes.Length)
                return false;

            for (var index = peerIdBytes.Length - 1; index >= 0; index--)
            {
                if (key[index] != peerIdBytes[index])
                    return false;
            }

            return true;
        }

        public void Dispose() => _db?.Dispose();
    }
}
