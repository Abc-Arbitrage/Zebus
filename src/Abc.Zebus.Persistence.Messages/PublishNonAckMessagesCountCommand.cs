using ProtoBuf;

namespace Abc.Zebus.Persistence.Messages
{
    [ProtoContract]
    [MessageTypeId("d2153c45-16c1-4813-bc09-3eefa32f6491")]
    public class PublishNonAckMessagesCountCommand : ICommand
    {
    }
}