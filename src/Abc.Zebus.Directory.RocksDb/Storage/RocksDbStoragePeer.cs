using Abc.Zebus.Routing;
using ProtoBuf;
using System;

namespace Abc.Zebus.Directory.RocksDb.Storage
{
    public partial class RocksDbPeerRepository
    {
        [ProtoContract]
        public class RocksDbStoragePeer
        {
            [ProtoMember(1)]
            public string PeerId { get; set; } = default!;

            [ProtoMember(2)]
            public string EndPoint { get; set; } = default!;

            [ProtoMember(3)]
            public bool IsUp { get; set; }

            [ProtoMember(4)]
            public bool IsResponding { get; set; }

            [ProtoMember(5)]
            public bool IsPersistent { get; set; }

            [ProtoMember(6)]
            public DateTime TimestampUtc { get; set; }

            [ProtoMember(7)]
            public bool HasDebuggerAttached { get; set; }

            [ProtoMember(8)]
            public Subscription[] StaticSubscriptions { get; set; } = default!;

            // For serialisation
            public RocksDbStoragePeer() { }

            public RocksDbStoragePeer(string peerId, string endPoint, bool isUp, bool isResponding, bool isPersistent, DateTime timestampUtc, bool hasDebuggerAttached, Subscription[] staticSubscriptions)
            {
                PeerId = peerId;
                EndPoint = endPoint;
                IsUp = isUp;
                IsResponding = isResponding;
                IsPersistent = isPersistent;
                TimestampUtc = timestampUtc;
                HasDebuggerAttached = hasDebuggerAttached;
                StaticSubscriptions = staticSubscriptions;
            }
        }
    }
}
