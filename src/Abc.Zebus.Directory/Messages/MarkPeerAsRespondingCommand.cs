using System;
using ProtoBuf;

namespace Abc.Zebus.Directory.Messages;

[ProtoContract]
public class MarkPeerAsRespondingInternalCommand : ICommand
{
    [ProtoMember(1, IsRequired = true)]
    public readonly PeerId PeerId;

    [ProtoMember(2, IsRequired = true)]
    public readonly DateTime TimestampUtc;

    public MarkPeerAsRespondingInternalCommand(PeerId peerId, DateTime timestampUtc)
    {
        PeerId = peerId;
        TimestampUtc = timestampUtc;
    }

    public override string ToString()
        => PeerId.ToString();
}
