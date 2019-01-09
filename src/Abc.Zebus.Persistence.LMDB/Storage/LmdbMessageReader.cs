using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Abc.Zebus.Persistence.Storage;
using Abc.Zebus.Transport;
using LightningDB;

namespace Abc.Zebus.Persistence.LMDB.Storage
{
    public class LmdbMessageReader : IMessageReader
    {
        private readonly PeerId _peerId;
        private readonly LightningTransaction _transaction;
        private readonly LightningDatabase _db;

        public LmdbMessageReader(LightningEnvironment environment, in PeerId peerId, string dbName)
        {
            _peerId = peerId;
            _transaction = environment.BeginTransaction(TransactionBeginFlags.ReadOnly);
            _db = _transaction.OpenDatabase(dbName);
        }

        public bool PeerExists()
        {
            using (var cursor = _transaction.CreateCursor(_db))
            {
                var key = LmdbStorage.CreateKeyBuffer(_peerId);
                LmdbStorage.FillKey(key, _peerId, 0, Guid.Empty);
                var peerExists = cursor.MoveToFirstAfter(key) && LmdbStorage.CompareStart(cursor.Current.Key, key, GetPeerPartLength(_peerId));
                return peerExists;
            }
        }

        public IEnumerable<TransportMessage> GetUnackedMessages()
        {
            var key = LmdbStorage.CreateKeyBuffer(_peerId);
            LmdbStorage.FillKey(key, _peerId, 0, Guid.Empty);

            var cursor = _transaction.CreateCursor(_db);
            var found = cursor.MoveToFirstAfter(key);
            if (!found)
                return Enumerable.Empty<TransportMessage>();

            return TransportMessages(cursor, key, _peerId);
        }

        private static IEnumerable<TransportMessage> TransportMessages(LightningCursor cursor, byte[] key, PeerId peerId)
        {
            var found = true;
            var peerString = peerId.ToString();
            var peerPartLength = GetPeerPartLength(peerId);
            while (found)
            {
                var currentKey = cursor.Current.Key;
                if (!LmdbStorage.CompareStart(currentKey, key, peerPartLength))
                    break;

                var currentValue = cursor.Current.Value;
                var transportMessage = TransportMessageDeserializer.Deserialize(currentValue);
                // var ticks = ReadTicksFromKey(currentKey, peerPartLength);
                // Console.WriteLine($"{peerString} - {ticks} - {transportMessage.Id}");
                yield return transportMessage;

                found = cursor.MoveNext();
            }
        }

        private static int GetPeerPartLength(PeerId peer) => Encoding.UTF8.GetByteCount(peer.ToString());

        private static long ReadTicksFromKey(byte[] currentKey, int peerPartLength)
        {
            var ticksBytes = new byte[sizeof(long)];
            Buffer.BlockCopy(currentKey, peerPartLength, ticksBytes, 0, sizeof(long));
            Array.Reverse(ticksBytes); // Change endianness
            var ticks = BitConverter.ToInt64(ticksBytes, 0);
            return ticks;
        }

        public void Dispose()
        {
            _transaction.Commit();
            _db.Dispose();
            _transaction.Dispose();
        }
    }
}