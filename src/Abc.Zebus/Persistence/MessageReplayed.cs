using System;
using Abc.Zebus.Transport;
using ProtoBuf;

namespace Abc.Zebus.Persistence
{
    [ProtoContract, Transient]
    public class MessageReplayed : IReplayEvent
    {
        public static readonly MessageTypeId TypeId = new MessageTypeId(typeof(MessageReplayed));

        [ProtoMember(1, IsRequired = true)]
        public Guid ReplayId { get; private set; }

        [ProtoMember(2, IsRequired = true)]
        public readonly TransportMessage Message;

        public MessageReplayed(Guid replayId, TransportMessage message)
        {
            ReplayId = replayId;
            Message = message;
        }
    }
}