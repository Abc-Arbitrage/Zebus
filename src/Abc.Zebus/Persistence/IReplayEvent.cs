using System;

namespace Abc.Zebus.Persistence
{
    public interface IReplayEvent : IEvent
    {
        Guid ReplayId { get; }
    }
}