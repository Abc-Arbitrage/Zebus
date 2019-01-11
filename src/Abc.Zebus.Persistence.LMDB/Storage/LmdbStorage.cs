using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Persistence.Storage;
using LightningDB;

namespace Abc.Zebus.Persistence.LMDB.Storage
{
    /// <summary>
    /// Key structure:
    /// -------------------------------------------------------------------
    /// |  PeerId (n bytes)  |  Ticks (8 bytes)  |  MessageId (16 bytes)  | 
    /// -------------------------------------------------------------------
    /// </summary>
    public class LmdbStorage : IStorage, IDisposable
    {
        private static readonly int _guidLength = Guid.Empty.ToByteArray().Length;
        private const string _outputPath = "data";

        private readonly string _dbName;
        private readonly ConcurrentDictionary<MessageId, bool> _outOfOrderAcks = new ConcurrentDictionary<MessageId, bool>();

        private LightningEnvironment _environment;

        public LmdbStorage(string dbName)
        {
            _dbName = dbName;
        }

        public int PersistenceQueueSize { get; } = 0;

        public void Start()
        {
            _environment = new LightningEnvironment(_outputPath) { MaxDatabases = 10 };
            _environment.Open();
            EnsureDatabasesAreCreated();
            ReadAllOutOfOrderAcks();
        }

        public void Stop()
        {
            Dispose();
        }

        public Task Write(IList<MatcherEntry> entriesToPersist)
        {
            using (var transaction = _environment.BeginTransaction())
            using (var db = transaction.OpenDatabase(GetMessagesDbName()))
            using (var acksDb = transaction.OpenDatabase(GetPendingAcksDbName()))
            using (var peersDb = transaction.OpenDatabase(GetPeersDbName()))
            using (var cursor = transaction.CreateCursor(db))
            {
                foreach (var (entry, ticks) in entriesToPersist.Select(x => (x, x.MessageId.GetDateTime().Ticks)))
                {
                    // Console.WriteLine($"{entry.PeerId.ToString()} - {ticks} - {entry.MessageId}");
                    var key = CreateKeyBuffer(entry.PeerId);
                    FillKey(key, entry.PeerId, ticks, entry.MessageId.Value);
                    if (entry.IsAck)
                    {
                        if (cursor.MoveTo(key))
                        {
                            cursor.Delete();
                        }
                        else
                        {
                            _outOfOrderAcks.TryAdd(entry.MessageId, default);
                            transaction.Put(acksDb, key, new byte[0]);
                        }
                    }
                    else
                    {
                        if (!_outOfOrderAcks.TryRemove(entry.MessageId, out _))
                            cursor.Put(key, entry.MessageBytes, CursorPutOptions.None);

                        // Otherwise ignore the message as it has already been acked
                    }
                }

                foreach (var entry in entriesToPersist.GroupBy(x => x.PeerId))
                {
                    UpdateNonAckedCounts(entry, transaction, peersDb);
                }

                transaction.Commit();
            }

            return Task.CompletedTask;
        }

        private static void UpdateNonAckedCounts(IGrouping<PeerId, MatcherEntry> entry, LightningTransaction transaction, LightningDatabase peersDb)
        {
            var nonAcked = entry.Aggregate(0, (s, e) => s + (e.IsAck ? -1 : 1));
            var peerKey = GetPeerKey(entry.Key);
            var alreadyExists = transaction.TryGet(peersDb, peerKey, out var currentNonAckedBytes);
            var currentNonAcked = alreadyExists ? BitConverter.ToInt32(currentNonAckedBytes, 0) : 0;
            transaction.Put(peersDb, peerKey, BitConverter.GetBytes(currentNonAcked + nonAcked));
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

        public IMessageReader CreateMessageReader(PeerId peerId)
        {
            using (var transaction = _environment.BeginTransaction())
            using (var peersDb = transaction.OpenDatabase(GetPeersDbName()))
            {
                if (!transaction.TryGet(peersDb, GetPeerKey(peerId), out _))
                    return null;
            }

            return new LmdbMessageReader(_environment, peerId, GetMessagesDbName());
        }

        public Task RemovePeer(PeerId peerId)
        {
            using (var transaction = _environment.BeginTransaction())
            using (var db = transaction.OpenDatabase(GetMessagesDbName()))
            using (var peersDb = transaction.OpenDatabase(GetPeersDbName()))
            {
                var key = CreateKeyBuffer(peerId);
                FillKey(key, peerId, 0, Guid.Empty);

                using (var cursor = transaction.CreateCursor(db))
                {
                    if (!cursor.MoveToFirstAfter(key))
                        return Task.CompletedTask;

                    byte[] currentKey;
                    var peerIdLength = peerId.ToString().Length;
                    do
                    {
                        cursor.Delete();
                        currentKey = cursor.Current.Key;
                    } while (cursor.MoveNext() && CompareStart(currentKey, key, peerIdLength));
                }

                transaction.Delete(peersDb, GetPeerKey(peerId));

                // TODO: remove out of order acks

                transaction.Commit();
            }
            return Task.CompletedTask;
        }

        public Dictionary<PeerId, int> GetNonAckedMessageCounts()
        {
            var nonAckedCounts = new Dictionary<PeerId, int>();
            using (var transaction = _environment.BeginTransaction())
            using (var peersDb = transaction.OpenDatabase(GetPeersDbName()))
            using (var cursor = transaction.CreateCursor(peersDb))
            {
                while (cursor.MoveNext())
                {
                    var peerId = ReadPeerKey(cursor.Current.Key); 
                    nonAckedCounts[peerId] = BitConverter.ToInt32(cursor.Current.Value, 0);
                }
            }

            return nonAckedCounts;
        }

        public void Dispose()
        {
            _environment?.Dispose();
            _environment = null;
        }

        public static byte[] CreateKeyBuffer(PeerId entryPeerId) => new byte[Encoding.UTF8.GetByteCount(entryPeerId.ToString()) + sizeof(long) + _guidLength];
        private string GetMessagesDbName() => _dbName + "-messages";
        private string GetPendingAcksDbName() => _dbName + "-acks";
        private string GetPeersDbName() => _dbName + "-peers";

        private void EnsureDatabasesAreCreated()
        {
            using (var transaction = _environment.BeginTransaction())
            using (var db = transaction.OpenDatabase(GetMessagesDbName(), new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create }))
            using (var acksDb = transaction.OpenDatabase(GetPendingAcksDbName(), new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create }))
            using (var peersDb = transaction.OpenDatabase(GetPeersDbName(), new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create }))
            {
                transaction.Commit();
            }
        }

        private void ReadAllOutOfOrderAcks()
        {
            using (var transaction = _environment.BeginTransaction())
            using (var acksDb = transaction.OpenDatabase(GetPendingAcksDbName()))
            using (var cursor = transaction.CreateCursor(acksDb))
            {
                while (cursor.MoveNext())
                {
                    var messageId = ExtractMessageId(cursor.Current.Key);
                    _outOfOrderAcks.TryAdd(messageId, default);
                }
            }
        }

        public static bool CompareStart(byte[] x, byte[] y, int length)
        {
            for (var index = length - 1; index >= 0; index--)
            {
                if (x[index] != y[index])
                    return false;
            }

            return true;
        }

        private static PeerId ReadPeerKey(byte[] keyBytes) => new PeerId(Encoding.UTF8.GetString(keyBytes));
        private static byte[] GetPeerKey(PeerId peerId) => Encoding.UTF8.GetBytes(peerId.ToString());
        private static MessageId ExtractMessageId(byte[] key) => new MessageId(new Guid(key.AsSpan(key.Length - _guidLength).ToArray()));
    }
}
