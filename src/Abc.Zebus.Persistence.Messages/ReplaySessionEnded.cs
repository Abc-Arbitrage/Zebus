using System;
using ProtoBuf;

namespace Abc.Zebus.Persistence.Messages
{
    [ProtoContract]
    [MessageTypeId("6d796ffc-6e89-43cf-9dfc-7e7c5cbc529c")]
    public class ReplaySessionEnded : IEvent
    {
        [ProtoMember(1, IsRequired = true)] public readonly PeerId Target;
        [ProtoMember(2, IsRequired = true)] public readonly Guid SessionId;
        
        private ReplaySessionEnded() { }
        
        public ReplaySessionEnded(PeerId target, Guid sessionId)
        {
            Target = target;
            SessionId = sessionId;
        }
    }
}