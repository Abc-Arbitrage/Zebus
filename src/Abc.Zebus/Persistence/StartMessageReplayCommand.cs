using System;
using ProtoBuf;

namespace Abc.Zebus.Persistence
{
    [ProtoContract, Transient, Infrastructure]
    public class StartMessageReplayCommand : ICommand
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly Guid ReplayId;

        public StartMessageReplayCommand(Guid replayId)
        {
            ReplayId = replayId;
        }
    }
}