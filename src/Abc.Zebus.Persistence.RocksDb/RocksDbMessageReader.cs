using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Abc.Zebus.Persistence.Storage;
using RocksDbSharp;

namespace Abc.Zebus.Persistence.RocksDb
{
    public class RocksDbMessageReader : IMessageReader
    {
        private readonly RocksDbSharp.RocksDb _db;
        private readonly PeerId _peerId;
        private readonly ColumnFamilyHandle _messagesColumnFamily;
        private Iterator? _iterator;

        public RocksDbMessageReader(RocksDbSharp.RocksDb db, in PeerId peerId, ColumnFamilyHandle messagesColumnFamily)
        {
            _db = db;
            _peerId = peerId;
            _messagesColumnFamily = messagesColumnFamily;
        }

        public IEnumerable<byte[]> GetUnackedMessages()
        {
            var key = RocksDbStorage.CreateKeyBuffer(_peerId);
            RocksDbStorage.FillKey(key, _peerId, 0, Guid.Empty);

            _iterator?.Dispose();
            _iterator = _db.NewIterator(_messagesColumnFamily);
            if (!_iterator.Seek(key).Valid())
                return Enumerable.Empty<byte[]>();

            return TransportMessages(_iterator, key, _peerId);
        }

        private static IEnumerable<byte[]> TransportMessages(Iterator iterator, byte[] key, PeerId peerId)
        {
            var found = true;
            var peerPartLength = GetPeerPartLength(peerId);
            while (found)
            {
                var currentKey = iterator.Key();
                if (!RocksDbStorage.CompareStart(currentKey, key, peerPartLength))
                    break;

                yield return iterator.Value();

                found = iterator.Next().Valid();
            }
        }

        private static int GetPeerPartLength(PeerId peer) => Encoding.UTF8.GetByteCount(peer.ToString());

        public void Dispose()
        {
            _iterator?.Dispose();
        }
    }
}
