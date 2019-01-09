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
        private readonly string _dbName;
        public const string OutputPath = "data";

        private LightningEnvironment _environment;
        private readonly ConcurrentDictionary<MessageId, bool> _outOfOrderAcks = new ConcurrentDictionary<MessageId, bool>();
        private static readonly int _guidLength = Guid.Empty.ToByteArray().Length;

        public int PersistenceQueueSize { get; } = 0;

        public LmdbStorage(string dbName)
        {
            _dbName = dbName;
        }

        public void Start()
        {
            _environment = new LightningEnvironment(OutputPath) { MaxDatabases = 10 };
            _environment.Open();
            EnsureDatabaseIsCreated();
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
                transaction.Commit();
            }

            return Task.CompletedTask;
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
            var messageReader = new LmdbMessageReader(_environment, peerId, GetMessagesDbName());
            if (!messageReader.PeerExists())
            {
                messageReader.Dispose();
                return null;
            }
            return messageReader;
        }

        public void RemovePeer(PeerId peerId)
        {
            using (var transaction = _environment.BeginTransaction())
            using (var db = transaction.OpenDatabase(GetMessagesDbName()))
            {
                var key = CreateKeyBuffer(peerId);
                FillKey(key, peerId, 0, Guid.Empty);

                using (var cursor = transaction.CreateCursor(db))
                {
                    var found = cursor.MoveToFirstAfter(key);
                    if (!found)
                        return;

                    var currentKey = cursor.Current.Key;
                    var length = peerId.ToString().Length;
                    do
                    {
                        cursor.Delete();

                    } while (cursor.MoveNext() && CompareStart(currentKey, key, length));
                }

                transaction.Commit();
            }
        }

        public Dictionary<PeerId, int> GetNonAckedMessageCountsForUpdatedPeers()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            _environment?.Dispose();
            _environment = null;
        }

        public static byte[] CreateKeyBuffer(PeerId entryPeerId) => new byte[Encoding.UTF8.GetByteCount(entryPeerId.ToString()) + sizeof(long) + _guidLength];
        private string GetMessagesDbName() => _dbName + "-messages"; 
        private string GetPendingAcksDbName() => _dbName + "-acks"; 

        private void EnsureDatabaseIsCreated()
        {
            using (var transaction = _environment.BeginTransaction())
            using (var db = transaction.OpenDatabase(GetMessagesDbName(), new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create }))
            using (var acksDb = transaction.OpenDatabase(GetPendingAcksDbName(), new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create }))
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
            for (int index = length - 1; index >= 0; index--)
            {
                if (x[index] != y[index])
                    return false;
            }

            return true;
        }

        private static MessageId ExtractMessageId(byte[] key) => new MessageId(new Guid(key.AsSpan(key.Length - _guidLength).ToArray()));
    }
}
