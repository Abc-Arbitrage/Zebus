﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Persistence.Storage;
using RocksDbSharp;
using StructureMap;

namespace Abc.Zebus.Persistence.RocksDb
{
    /// <summary>
    /// Key structure:
    /// -------------------------------------------------------------------
    /// |  PeerId (n bytes)  |  Ticks (8 bytes)  |  MessageId (16 bytes)  | 
    /// -------------------------------------------------------------------
    /// </summary>
    public class RocksDbStorage : IStorage, IDisposable
    {
        private static readonly int _guidLength = Guid.Empty.ToByteArray().Length;

        private readonly ConcurrentDictionary<MessageId, bool> _outOfOrderAcks = new ConcurrentDictionary<MessageId, bool>();

        private RocksDbSharp.RocksDb _db;
        private readonly string _dbName;
        private ColumnFamilyHandle _messagesColumnFamily;
        private ColumnFamilyHandle _peersColumnFamily;
        private ColumnFamilyHandle _acksColumnFamily;

        [DefaultConstructor]
        public RocksDbStorage()
            : this("zebus-persistence")
        {
        }

        public RocksDbStorage(string dbName)
        {
            _dbName = dbName;
        }

        public int PersistenceQueueSize { get; } = 0;

        public void Start()
        {
            var options = new DbOptions().SetCreateIfMissing()
                                         .SetCreateMissingColumnFamilies()
                                         .SetMaxBackgroundCompactions(4)
                                         .SetMaxBackgroundFlushes(2)
                                         .SetBytesPerSync(1048576);
            var columnFamilies = new ColumnFamilies();
            columnFamilies.Add("Messages", ColumnFamilyOptions());
            columnFamilies.Add("Peers", ColumnFamilyOptions());
            columnFamilies.Add("Acks", ColumnFamilyOptions());
            _db = RocksDbSharp.RocksDb.Open(options, _dbName, columnFamilies);

            _messagesColumnFamily = _db.GetColumnFamily("Messages");
            _peersColumnFamily = _db.GetColumnFamily("Peers");
            _acksColumnFamily = _db.GetColumnFamily("Acks");

            ColumnFamilyOptions ColumnFamilyOptions() => new ColumnFamilyOptions().SetCompression(CompressionTypeEnum.rocksdb_no_compression)
                                                                                  .SetLevelCompactionDynamicLevelBytes(true)
                                                                                  .SetArenaBlockSize(16 * 1024);
        }

        public void Stop() => Dispose();

        public void Dispose() => _db?.Dispose();

        public Task Write(IList<MatcherEntry> entriesToPersist)
        {
            foreach (var entry in entriesToPersist)
            {
                var key = CreateKeyBuffer(entry.PeerId);
                FillKey(key, entry.PeerId, entry.MessageId.GetDateTime().Ticks, entry.MessageId.Value);
                if (entry.IsAck)
                {
                    var bytes = _db.Get(key, _messagesColumnFamily);
                    if (bytes != null)
                    {
                        _db.Remove(key, _messagesColumnFamily);
                    }
                    else
                    {
                        _outOfOrderAcks.TryAdd(entry.MessageId, default);
                        _db.Put(key, Array.Empty<byte>(), _acksColumnFamily);
                    }
                }
                else
                {
                    if (!_outOfOrderAcks.TryRemove(entry.MessageId, out _))
                        _db.Put(key, entry.MessageBytes, _messagesColumnFamily);

                    // Otherwise ignore the message as it has already been acked
                }
            }

            foreach (var entry in entriesToPersist.GroupBy(x => x.PeerId))
            {
                UpdateNonAckedCounts(entry);
            }

            return Task.CompletedTask;
        }

        private void UpdateNonAckedCounts(IGrouping<PeerId, MatcherEntry> entry)
        {
            var nonAcked = entry.Aggregate(0, (s, e) => s + (e.IsAck ? -1 : 1));
            var peerKey = GetPeerKey(entry.Key);
            using (var iterator = _db.NewIterator(_peersColumnFamily))//, new ReadOptions().SetTotalOrderSeek(true)))
            {
                // TODO: figure out why Seek() returns true for a different key
                var alreadyExists = iterator.Seek(peerKey).Valid() && CompareStart(iterator.Key(), peerKey, peerKey.Length);
                var currentNonAcked = alreadyExists ? BitConverter.ToInt32(iterator.Value(), 0) : 0;

                var value = currentNonAcked + nonAcked;
                _db.Put(peerKey, BitConverter.GetBytes(value), _peersColumnFamily);
            }
        }

        public IMessageReader CreateMessageReader(PeerId peerId)
        {
            using (var iterator = _db.NewIterator(_peersColumnFamily))
            {
                if (!iterator.Seek(GetPeerKey(peerId)).Valid())
                    return null;
            }

            return new RocksDbMessageReader(_db, peerId, _messagesColumnFamily);
        }

        public Task RemovePeer(PeerId peerId)
        {
            var key = CreateKeyBuffer(peerId);
            FillKey(key, peerId, 0, Guid.Empty);

            using (var cursor = _db.NewIterator(_messagesColumnFamily))
            {
                if (!cursor.Seek(key).Valid())
                    return Task.CompletedTask;

                var peerIdLength = peerId.ToString().Length;
                byte[] currentKey;
                do
                {
                    currentKey = cursor.Key();
                    _db.Remove(currentKey);
                    cursor.Next();
                } while (cursor.Valid() && CompareStart(currentKey, key, peerIdLength));
            }

            _db.Remove(GetPeerKey(peerId), _peersColumnFamily);

            // TODO: remove out of order acks
            return Task.CompletedTask;
        }

        public Dictionary<PeerId, int> GetNonAckedMessageCounts()
        {
            var nonAckedCounts = new Dictionary<PeerId, int>();
            using (var cursor = _db.NewIterator(_peersColumnFamily))
            {
                for (cursor.SeekToFirst(); cursor.Valid(); cursor.Next())
                {
                    var peerId = ReadPeerKey(cursor.Key());
                    nonAckedCounts[peerId] = BitConverter.ToInt32(cursor.Value(), 0);
                }
            }

            return nonAckedCounts;
        }

        public static void FillKey(byte[] key, PeerId peerId, long ticks, Guid messageId)
        {
            var peerPart = Encoding.UTF8.GetBytes(peerId.ToString());
            Buffer.BlockCopy(peerPart, 0, key, 0, peerPart.Length);

            var tickPart = BitConverter.GetBytes(ticks);
            Array.Reverse(tickPart); // change endianness
            Buffer.BlockCopy(tickPart, 0, key, peerPart.Length, sizeof(long));

            var messageIdPart = messageId.ToByteArray();
            Buffer.BlockCopy(messageIdPart, 0, key, peerPart.Length + sizeof(long), _guidLength);
        }

        private static PeerId ReadPeerKey(byte[] keyBytes) => new PeerId(Encoding.UTF8.GetString(keyBytes));
        private static byte[] GetPeerKey(PeerId peerId) => Encoding.UTF8.GetBytes(peerId.ToString());
        public static byte[] CreateKeyBuffer(PeerId entryPeerId) => new byte[Encoding.UTF8.GetByteCount(entryPeerId.ToString()) + sizeof(long) + _guidLength];

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
    }
}
