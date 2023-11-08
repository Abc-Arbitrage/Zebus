using System;
using Abc.Zebus.Util;
using ProtoBuf;

namespace Abc.Zebus.Directory;

[ProtoContract, Transient]
public class PeerResponding : IEvent
{
    [ProtoMember(1, IsRequired = true)]
    public readonly PeerId PeerId;

    [ProtoMember(2, IsRequired = false)]
    public readonly DateTime? TimestampUtc;

    public PeerResponding(PeerId peerId, DateTime? timestampUtc = null)
    {
        PeerId = peerId;
        TimestampUtc = timestampUtc ?? SystemDateTime.UtcNow;
    }

    public override string ToString()
        => PeerId.ToString();
}
