using System.Collections.Generic;
using System.Linq;
using Abc.Zebus.Transport;
using ProtoBuf;

namespace Abc.Zebus.Persistence;

[ProtoContract, Transient]
public class PersistMessageCommand : ICommand
{
    [ProtoMember(1, IsRequired = true)]
    public readonly TransportMessage TransportMessage;

    [ProtoMember(2, IsRequired = true)]
    public readonly List<PeerId> Targets;

    public PersistMessageCommand(TransportMessage transportMessage, params PeerId[] targets) : this(transportMessage, targets.ToList())
    {
    }

    public PersistMessageCommand(TransportMessage transportMessage, List<PeerId> targets)
    {
        TransportMessage = transportMessage;
        Targets = targets;
    }

    public override string ToString()
        => "PersistCommand for: " + TransportMessage.MessageTypeId + " " + TransportMessage.Id.Value;
}
