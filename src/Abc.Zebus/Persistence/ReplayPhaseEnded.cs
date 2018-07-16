using System;
using ProtoBuf;

namespace Abc.Zebus.Persistence
{
    [ProtoContract, Transient]
    public class ReplayPhaseEnded : IReplayEvent
    {
        public static readonly MessageTypeId TypeId = new MessageTypeId(typeof(ReplayPhaseEnded));

        [ProtoMember(1, IsRequired = true)]
        public Guid ReplayId { get; private set; }

        public ReplayPhaseEnded(Guid replayId)
        {
            ReplayId = replayId;
        }
    }
}