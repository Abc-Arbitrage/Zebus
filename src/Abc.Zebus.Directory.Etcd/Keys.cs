using System.Collections;
using System.Collections.Generic;

namespace Abc.Zebus.Directory.Etcd
{
    public static class Keys
    {
        public static string Peers(string prefix)
            => CreateKey(prefix, "peers");

        public static string EncodePeer(string prefix, Peer peer)
            => EncodePeer(prefix, peer.Id);

        public static string EncodePeer(string prefix, PeerId peerId)
            => CreateKey(prefix, $"peers/{peerId}");

        public static string EncodeSubscriptionForMessage(string prefix, MessageTypeId messageTypeId)
            => CreateKey(prefix, $"subscriptions/{messageTypeId}");

        public static string EncodeSubscriptionForPeer(string prefix, Peer peer, MessageTypeId messageTypeId)
            => SubscriptionForPeer(prefix, peer.Id, messageTypeId);

        public static string SubscriptionForPeer(string prefix, PeerId peerId, MessageTypeId messageTypeId)
            => CreateKey(prefix, $"subscriptions/{messageTypeId}/{peerId}");

        public static bool TryDecodePeer(string prefix, string key, out PeerId peerId)
        {
            peerId = default;

            var reader = new KeyReader(key);
            if (!reader.Expect(prefix))
                return false;

            if (!reader.Expect("peers"))
                return false;

            if (!reader.TryNext(out var value))
                return false;

            peerId = new PeerId(value);
            return true;
        }

        public static (PeerId PeerId, MessageTypeId MessageTypeId)? DecodeSubscription(string prefix, string key)
        {
            var reader = new KeyReader(key);
            if (!reader.Expect(prefix))
                return null;

            if (!reader.Expect("subscriptions"))
                return null;

            if (!reader.TryNext(out var messageTypeId))
                return null;

            if (!reader.TryNext(out var peerId))
                return null;

            return (new PeerId(peerId), new MessageTypeId(messageTypeId));
        }

        private static string CreateKey(string prefix, string key)
            => $"{prefix}/{key}";

        private struct KeyReader
        {
            private readonly string[] _parts;
            private uint _index;

            public KeyReader(string key)
            {
                _parts = key.Split('/');
                _index = 0;
            }

            public bool TryNext(out string? part)
            {
                part = null;
                if (_index >= _parts.Length)
                    return false;

                part = _parts[_index];
                ++_index;
                return true;
            }

            public bool Expect(string value)
            {
                if (_index >= _parts.Length)
                    return false;

                var part = _parts[_index];
                if (part == value)
                {
                    ++_index;
                    return true;
                }

                return false;
            }
        }
    }
}
