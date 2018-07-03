using System;
using ProtoBuf;

namespace Abc.Zebus.Persistence.Messages
{
    [ProtoContract]
    [MessageTypeId("369ef940-1f33-4590-a33e-c722a468d373")]
    public class ReplaySessionStarted : IEvent
    {
        [ProtoMember(1, IsRequired = true)] public readonly PeerId Target;
        [ProtoMember(2, IsRequired = true)] public readonly Guid SessionId;
        
        private ReplaySessionStarted() { }
        
        public ReplaySessionStarted(PeerId target, Guid sessionId)
        {
            Target = target;
            SessionId = sessionId;
        }
    }
}