using ProtoBuf;

namespace Abc.Zebus.Persistence
{
    [ProtoContract, Transient]
    public class MessageHandled : IEvent
    {
        [ProtoMember(1, IsRequired = true)]
        public readonly MessageId MessageId;

        public MessageHandled(MessageId messageId)
        {
            MessageId = messageId;
        }

        public override string ToString()
        {
            return "MessageHandled for: " + MessageId.Value;
        }
    }
}