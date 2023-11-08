using System;
using JetBrains.Annotations;
using ProtoBuf;

namespace Abc.Zebus;

[ProtoContract]
public class Peer
{
    [ProtoMember(1, IsRequired = true)]
    public readonly PeerId Id;

    [ProtoMember(2, IsRequired = true)]
    public string EndPoint;

    [ProtoMember(3, IsRequired = true)]
    public bool IsUp;

    [ProtoMember(4, IsRequired = false)]
    public bool IsResponding;

    public Peer(PeerId id, string endPoint, bool isUp = true) : this(id, endPoint, isUp, isUp)
    {
    }

    public Peer(Peer other) : this(other.Id, other.EndPoint, other.IsUp, other.IsResponding)
    {
    }

    public Peer(PeerId id, string endPoint, bool isUp, bool isResponding)
    {
        Id = id;
        EndPoint = endPoint;
        IsUp = isUp;
        IsResponding = isResponding;
    }

    [UsedImplicitly]
    private Peer()
    {
        EndPoint = default!;
    }

    public override string ToString() => $"{Id}, {EndPoint}";

    public string GetMachineNameFromEndPoint()
        => new Uri(EndPoint).Host;
}
