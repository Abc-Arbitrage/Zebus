using ProtoBuf;

namespace Abc.Zebus.Persistence.Messages
{
    [ProtoContract]
    [MessageTypeId("51c68194-2ddb-4704-8ef9-f01f7795c5a0")]
    public class NonAckMessagesCountChanged : IEvent
    {
        [ProtoMember(1, IsRequired = false)] public readonly NonAckMessage[] NonAckMessages;

        public NonAckMessagesCountChanged()
        {
            NonAckMessages = new NonAckMessage[0];
        }
        
        public NonAckMessagesCountChanged(NonAckMessage[] nonAckMessages)
        {
            NonAckMessages = nonAckMessages;
        }
    }
}
