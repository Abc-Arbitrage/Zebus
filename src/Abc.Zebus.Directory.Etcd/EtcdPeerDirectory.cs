using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Routing;
using Abc.Zebus.Util;
using dotnet_etcd;
using Etcdserverpb;
using Google.Protobuf;
using log4net;
using Mvccpb;
using ProtoBuf;

namespace Abc.Zebus.Directory.Etcd
{
    /// <summary>
    /// A PeerDirectory implementation based on `etcd`.
    ///
    /// The basic idea is that the whole state of the directory is maintained by `etcd`, which means that we do not
    /// need a Directory server anymore.
    /// We also do not need to know the entire directory (e.g peers and subscriptions) when attempting to send a message
    /// to a given peer.
    ///
    /// When retrieving the peers for a particular message binding, we first check our internal cache for a list of
    /// peers and binding keys that we might already know. If the entry of the cache for a particular `MessageTypeId` is
    /// empty, we retrieve the current subscriptions for the `MessageTypeId` inside `etcd` and then use a `watch` to get
    /// notified when future subscriptions are added or removed for the `MessageTypeId`
    ///
    /// The structure of keys inside `etcd` is the following:
    ///     Peers are added in `prefix/peers/` folder, e.g
    ///               Key                   Value
    ///         bus/peers/Abc.Peer.0    PeerDescriptor
    ///         bus/peers/Abc.Peer.1    PeerDescriptor
    ///
    ///     Subscriptions are added in `prefix/subscriptions/{MessageTypeId}` folder e.g
    ///                     Key                                 Value
    ///         bus/subscriptions/TestCommand/Abc.Peer.0        BindingKey0, BindingKey1, ...
    ///         bus/subscriptions/TestEvent/Abc.Peer.0          BindingKey0, BindingKey1, ...
    ///         bus/subscriptions/TestEvent/Abc.Peer.1          BindingKey0, BindingKey1, ...
    /// </summary>
    public class EtcdPeerDirectory : IPeerDirectory
    {
        private static readonly ILog _logger = LogManager.GetLogger(nameof(EtcdClient));

        public event Action<PeerId, PeerUpdateAction>? PeerUpdated;
        public event Action<PeerId, IReadOnlyList<Subscription>>? PeerSubscriptionsUpdated;

        private readonly ConcurrentDictionary<PeerId, PeerDescriptor> _peers = new();
        private readonly ConcurrentDictionary<MessageTypeId, PeerSubscriptionTree> _subscriptionsIndex = new();

        public TimeSpan TimeSinceLastPing { get; }

        private readonly UniqueTimestampProvider _timestampProvider = new(10);

        private readonly EtcdClient _etcd;
        private readonly IEtcdConfiguration _configuration;

        private Peer _self = default!;

        public EtcdPeerDirectory(IEtcdConfiguration configuration)
        {
            _etcd = CreateEtcdClient(configuration);
            _etcd = new EtcdClient(CreateEtcdConnectionString(configuration));
            _configuration = configuration;
        }

        public async Task RegisterAsync(IBus bus, Peer self, IEnumerable<Subscription> subscriptions)
        {
            var subscriptionsArray = subscriptions.ToArray();
            var descriptor = CreateDescriptor(self, subscriptionsArray);

            using var stream = new MemoryStream();
            Serializer.Serialize(stream, descriptor);

            stream.Seek(0, SeekOrigin.Begin);

            var peerKey = Keys.EncodePeer(_configuration.Prefix, self);
            var putResult = await _etcd.PutAsync(new PutRequest
            {
                Key = ByteString.CopyFromUtf8(peerKey),
                Value = ByteString.FromStream(stream)
            });

            var version = putResult.Header.Revision;

            foreach (var subscription in subscriptionsArray)
            {
                var subscriptionKey = Keys.EncodeSubscriptionForPeer(_configuration.Prefix, self, subscription.MessageTypeId);
                var bindingKey = SerializeBindingKeys(subscription.BindingKey);

                putResult = await _etcd.PutAsync(new PutRequest
                {
                    Key = ByteString.CopyFromUtf8(subscriptionKey),
                    Value = ByteString.CopyFrom(bindingKey)
                });
            }

            PeerSubscriptionsUpdated?.Invoke(self.Id, Array.Empty<Subscription>());

            await WatchPeers();

            _self = self;
            _logger.Info($"Registered to etcd. Version {version}");
        }

        private async Task WatchPeers()
        {
            var peersFolder = Keys.Peers(_configuration.Prefix);
            var request = new WatchRequest()
            {
                CreateRequest = new WatchCreateRequest()
                {
                    Key = EtcdClient.GetStringByteForRangeRequests(peersFolder),
                    RangeEnd = ByteString.CopyFromUtf8(EtcdClient.GetRangeEnd(peersFolder))
                }
            };

            await _etcd.WatchRange(request, OnPeersUpdated);
        }

        public async Task UpdateSubscriptionsAsync(IBus bus, IEnumerable<SubscriptionsForType> subscriptionsForTypes)
        {
            foreach (var subscriptionForType in subscriptionsForTypes)
            {
                var messageTypeId = subscriptionForType.MessageTypeId;
                var bindingKeys = subscriptionForType.BindingKeys;
                var subscriptionKey = Keys.SubscriptionForPeer(_configuration.Prefix, _self.Id, messageTypeId);

                if (bindingKeys == null || bindingKeys.Length == 0)
                {
                    var result = await _etcd.DeleteAsync(subscriptionKey);
                }
                else
                {
                    var value = SerializeBindingKeys(bindingKeys);
                    await _etcd.PutAsync(new PutRequest
                    {
                        Key = ByteString.CopyFromUtf8(subscriptionKey),
                        Value = ByteString.CopyFrom(value.AsSpan())
                    });
                }
            }
        }

        public Task UnregisterAsync(IBus bus)
        {
            throw new NotImplementedException();
        }

        public IList<Peer> GetPeersHandlingMessage(IMessage message)
            => GetPeersHandlingMessage(MessageBinding.FromMessage(message));

        public IList<Peer> GetPeersHandlingMessage(MessageBinding messageBinding)
        {
            var subscriptionTree = _subscriptionsIndex.GetOrAdd(messageBinding.MessageTypeId, CreateAndWatchSubscriptionTree);
            return subscriptionTree.GetPeers(messageBinding.RoutingKey);
        }

        public bool IsPersistent(PeerId peerId)
        {
            throw new NotImplementedException();
        }

        public Peer? GetPeer(PeerId peerId)
            => GetPeerDescriptor(peerId)?.Peer;

        public void EnableSubscriptionsUpdatedFor(IEnumerable<Type> types)
        {
            throw new NotImplementedException();
        }

        public PeerDescriptor? GetPeerDescriptor(PeerId peerId)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<PeerDescriptor> GetPeerDescriptors()
        {
            throw new NotImplementedException();
        }

        private PeerSubscriptionTree CreateAndWatchSubscriptionTree(MessageTypeId messageTypeId)
        {
            var subscriptionKey = Keys.EncodeSubscriptionForMessage(_configuration.Prefix, messageTypeId);
            var subscriptionsResult = _etcd.GetRange(subscriptionKey);

            var tree = new PeerSubscriptionTree();

            foreach (var kv in subscriptionsResult.Kvs)
            {
                var key = kv.Key.ToStringUtf8();
                var peerDescriptor = GetPeerFromSubscription(key);
                if (peerDescriptor == null)
                {
                    _logger.Warn($"Failed to retrieve peer from subscription key {key}");
                    continue;
                }

                var bindingKeys = DeserializeBindingKeys(kv.Value.ToByteArray());
                foreach (var bindingKey in bindingKeys)
                {
                    tree.Add(peerDescriptor.Peer, bindingKey);
                }
            }

            Task.Run(() => _etcd.WatchRange(subscriptionKey, resp => OnSubscriptionUpdated(resp, messageTypeId)));
            return tree;
        }

        private void OnPeersUpdated(WatchResponse response)
        {
            foreach (var watchEvent in response.Events)
            {
                var kv = watchEvent.Kv;
                var key = kv.Key.ToStringUtf8();

                if (!Keys.TryDecodePeer(_configuration.Prefix, key, out var peerId))
                {
                    _logger.Warn($"Invalid key for peer {key}");
                    continue;
                }

                var peerDescriptor = Deserialize<PeerDescriptor>(kv.Value.ToByteArray());
                if (peerDescriptor == null)
                    continue;

                switch (watchEvent.Type)
                {
                    case Event.Types.EventType.Put:
                    {
                        _peers.AddOrUpdate(peerId, _ => peerDescriptor, (_, _) =>  peerDescriptor);

                        if (kv.Version == 1)
                            PeerUpdated?.Invoke(peerId, PeerUpdateAction.Started);
                        else
                            PeerUpdated?.Invoke(peerId, PeerUpdateAction.Updated);
                        break;
                    }
                    case Event.Types.EventType.Delete:
                        _peers.TryRemove(peerId, out _);
                        PeerUpdated?.Invoke(peerId, PeerUpdateAction.Stopped);
                        break;
                }
            }
        }

        private void OnSubscriptionUpdated(WatchResponse watchResponse, MessageTypeId messageTypeId)
        {
            if (!_subscriptionsIndex.TryGetValue(messageTypeId, out var tree))
                return;

            foreach (var watchEvent in watchResponse.Events)
            {
                var kv = watchEvent.Kv;
                var key = kv.Key.ToStringUtf8();

                var peerDescriptor = GetPeerFromSubscription(key);
                if (peerDescriptor == null)
                {
                    _logger.Warn($"Failed to retrieve peer from subscription key {key}");
                    continue;
                }

                var bindingKeys = DeserializeBindingKeys(kv.Value.ToByteArray());

                var peer = peerDescriptor.Peer;
                switch (watchEvent.Type)
                {
                    case Event.Types.EventType.Put:
                        foreach (var bindingKey in bindingKeys)
                        {
                            tree.Add(peer, bindingKey);
                            _logger.Debug($"Subscription added for {messageTypeId} ({bindingKey}) on peer {peerDescriptor.PeerId}");
                        }
                        break;
                    case Event.Types.EventType.Delete:
                        _logger.Debug($"Subscription removed for {messageTypeId} on peer {peerDescriptor.PeerId}");
                        break;
                }
            }
        }

        private PeerDescriptor CreateDescriptor(Peer peer, Subscription[] subscriptions)
        {
            return new PeerDescriptor(peer.Id, peer.EndPoint, _configuration.IsPersistent, true, true, _timestampProvider.NextUtcTimestamp(), subscriptions)
            {
                HasDebuggerAttached = Debugger.IsAttached
            };
        }

        private static EtcdClient CreateEtcdClient(IEtcdConfiguration configuration)
        {
            var client = new EtcdClient(CreateEtcdConnectionString(configuration));
            if (!string.IsNullOrEmpty(configuration.Username) && !string.IsNullOrEmpty(configuration.Password))
            {
                var authResult = client.Authenticate(new AuthenticateRequest
                {
                    Name = configuration.Username,
                    Password = configuration.Password
                });
            }

            return client;
        }

        private static string CreateEtcdConnectionString(IBusConfiguration configuration)
            => string.Join(",", configuration.DirectoryServiceEndPoints);

        private PeerDescriptor? GetPeerFromSubscription(string subscriptionKey)
        {
            var decodedKey = Keys.DecodeSubscription(_configuration.Prefix, subscriptionKey);
            if (decodedKey == null)
                return null;

            var peerId = decodedKey.Value.PeerId;
            var peerKey = Keys.EncodePeer(_configuration.Prefix, peerId);

            var peerResult = _etcd.Get(peerKey);
            var peerDescriptorBytes = peerResult.Kvs.FirstOrDefault()?.Value;

            if (peerDescriptorBytes == null)
                return null;

            return Deserialize<PeerDescriptor>(peerDescriptorBytes.ToByteArray());
        }

        private static T? Deserialize<T>(byte[] bytes)
            where T: class
        {
            try
            {
                return Serializer.Deserialize<T>(new MemoryStream(bytes));
            }
            catch (Exception e)
            {
                _logger.Error($"Error deserializing buffer {BitConverter.ToString(bytes)} into {nameof(T)}: {e}");
                return null;
            }
        }

        private static byte[] SerializeBindingKeys(params BindingKey[] bindingKeys)
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
    }
}
