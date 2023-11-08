using System;
using ProtoBuf;

namespace Abc.Zebus.Persistence;

[ProtoContract, Transient]
public class SafetyPhaseEnded : IReplayEvent
{
    public static readonly MessageTypeId TypeId = new(typeof(SafetyPhaseEnded));

    [ProtoMember(1, IsRequired = true)]
    public Guid ReplayId { get; private set; }

    public SafetyPhaseEnded(Guid replayId)
    {
        ReplayId = replayId;
    }
}
