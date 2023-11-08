using System;
using System.Linq;
using JetBrains.Annotations;
using ProtoBuf;

namespace Abc.Zebus.Directory;

[ProtoContract]
public class PeerDescriptor
{
    [ProtoMember(1, IsRequired = true)]
    public readonly Peer Peer;

    [ProtoMember(2, IsRequired = true)]
    public Subscription[] Subscriptions { get; set; }

    [ProtoMember(3, IsRequired = true)]
    public bool IsPersistent { get; set; }

    [ProtoMember(4, IsRequired = false)]
    public DateTime? TimestampUtc { get; set; }

    [ProtoMember(5, IsRequired = false)]
    public bool HasDebuggerAttached { get; set; }

    public PeerDescriptor(PeerId id, string endPoint, bool isPersistent, bool isUp, bool isResponding, DateTime timestampUtc, params Subscription[] subscriptions)
    {
        Peer = new Peer(id, endPoint, isUp, isResponding);
        Subscriptions = subscriptions;
        IsPersistent = isPersistent;
        TimestampUtc = timestampUtc;
    }

    internal PeerDescriptor(PeerDescriptor other)
    {
        Peer = new Peer(other.Peer);
        Subscriptions = other.Subscriptions?.ToArray() ?? Array.Empty<Subscription>();
        IsPersistent = other.IsPersistent;
        TimestampUtc = other.TimestampUtc;
        HasDebuggerAttached = other.HasDebuggerAttached;
    }

    [UsedImplicitly]
    private PeerDescriptor()
    {
        Peer = default!;
        Subscriptions = Array.Empty<Subscription>();
    }

    public PeerId PeerId => Peer.Id;

    public override string ToString()
        => Peer.ToString();
}
