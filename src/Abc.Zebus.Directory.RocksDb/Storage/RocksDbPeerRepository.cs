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

namespace Abc.Zebus.Directory.RocksDb.Storage
{
    public partial class RocksDbPeerRepository : IPeerRepository, IDisposable
    {
        private readonly string _databaseDirectoryPath;
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
        /// --------------------------------------------------
        /// |  PeerId (n bytes)  |  MessageTypeId (n bytes)  | 
        /// --------------------------------------------------
        /// </summary>
        private ColumnFamilyHandle _subscriptionsColumnFamily;

        [DefaultConstructor]
        public RocksDbPeerRepository()
            : this(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database"))
        {
        }

        public RocksDbPeerRepository(string databaseDirectoryPath)
        {
            _databaseDirectoryPath = databaseDirectoryPath;
        }

        public void Start()
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

            _db = RocksDbSharp.RocksDb.Open(options, _databaseDirectoryPath, columnFamilies);

            _peersColumnFamily = _db.GetColumnFamily("Peers");
            _subscriptionsColumnFamily = _db.GetColumnFamily("Subscriptions");

            ColumnFamilyOptions ColumnFamilyOptions() => new ColumnFamilyOptions().SetCompression(CompressionTypeEnum.rocksdb_no_compression)
                                                                                  .SetLevelCompactionDynamicLevelBytes(true)
                                                                                  .SetArenaBlockSize(16 * 1024);
        }

        public void AddDynamicSubscriptionsForTypes(PeerId peerId, DateTime timestampUtc, SubscriptionsForType[] subscriptionsForTypes)
        {
            foreach (var subscription in subscriptionsForTypes)
            {
                var key = GetSubscriptionKey(peerId, subscription.MessageTypeId);
                var value = SerializeBindingKeys(subscription.BindingKeys);
                _db.Put(key, value, _subscriptionsColumnFamily);
            }
        }

        public void AddOrUpdatePeer(PeerDescriptor peerDescriptor)
        {
            var key = GetPeerKey(peerDescriptor.PeerId);
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
            var peerStorageBytes = _db.Get(GetPeerKey(peerId), _peersColumnFamily);
            var peerStorage = Serializer.Deserialize<RocksDbStoragePeer>(new MemoryStream(peerStorageBytes));
            var staticSubscriptions = peerStorage.StaticSubscriptions;
            var subscriptions = staticSubscriptions.Concat(GetDynamicSubscriptions(peerId));
            return new PeerDescriptor(peerId, peerStorage.EndPoint, peerStorage.IsPersistent, peerStorage.IsUp, peerStorage.IsResponding, peerStorage.TimestampUtc, subscriptions.ToArray());
        }

        private Subscription[] GetDynamicSubscriptions(PeerId peerId)
        {
            var bindingKeys = new List<(MessageTypeId, BindingKey[])>();
            var key = GetSubscriptionKey(peerId);
            using (var cursor = _db.NewIterator(_subscriptionsColumnFamily))
            {
                if (!cursor.Seek(key).Valid())
                    return Array.Empty<Subscription>();

                var peerIdLength = peerId.ToString().Length;
                byte[] currentKey;
                do
                {
                    currentKey = cursor.Key();
                    var messageTypeId = GetMessageTypeIdFromKey(currentKey, peerId);
                    bindingKeys.Add((messageTypeId, DeserializeBindingKeys(cursor.Value())));
                    cursor.Next();
                } while (cursor.Valid() && CompareStart(currentKey, key, peerIdLength));
            }
            var subscriptions = bindingKeys.SelectMany(y => y.Item2.Select(bindingKey => new Subscription(y.Item1, bindingKey))).ToArray();
            return subscriptions;
        }

        private MessageTypeId GetMessageTypeIdFromKey(byte[] currentKey, PeerId peerId)
        {
            var peerIdByteCount = Encoding.UTF8.GetByteCount(peerId.ToString());
            if (currentKey.Length <= peerIdByteCount)
                throw new ApplicationException("Key does not contain message type id");

            return new MessageTypeId(Encoding.UTF8.GetString(currentKey, peerIdByteCount, currentKey.Length - peerIdByteCount));
        }

        public IEnumerable<PeerDescriptor> GetPeers(bool loadDynamicSubscriptions = true) => throw new NotImplementedException();
        public bool? IsPersistent(PeerId peerId) => throw new NotImplementedException();
        public void RemoveAllDynamicSubscriptionsForPeer(PeerId peerId, DateTime timestampUtc) => throw new NotImplementedException();
        public void RemoveDynamicSubscriptionsForTypes(PeerId peerId, DateTime timestampUtc, MessageTypeId[] messageTypeIds) => throw new NotImplementedException();
        public void RemovePeer(PeerId peerId) => throw new NotImplementedException();
        public void SetPeerResponding(PeerId peerId, bool isResponding) => throw new NotImplementedException();
        private byte[] GetPeerKey(PeerId peerId) => Encoding.UTF8.GetBytes(peerId.ToString());

        private byte[] GetSubscriptionKey(PeerId peerId, MessageTypeId messageTypeId)
        {
            var peerPart = Encoding.UTF8.GetBytes(peerId.ToString());
            var messageTypeIdPart = Encoding.UTF8.GetBytes(messageTypeId.FullName);

            byte[] key = new byte[peerPart.Length + messageTypeIdPart.Length];
            Buffer.BlockCopy(peerPart, 0, key, 0, peerPart.Length);
            Buffer.BlockCopy(messageTypeIdPart, 0, key, peerPart.Length, messageTypeIdPart.Length);

            return key;
        }

        private byte[] GetSubscriptionKey(PeerId peerId)
        {
            var peerPart = Encoding.UTF8.GetBytes(peerId.ToString());

            byte[] key = new byte[peerPart.Length];
            Buffer.BlockCopy(peerPart, 0, key, 0, peerPart.Length);

            return key;
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
                        binaryWriter.Write(bindingKey.GetPart(partIndex));
                }
                return memoryStream.ToArray();
            }
        }
        public static bool CompareStart(byte[] x, byte[] y, int length)
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
