using JetBrains.Annotations;
using ProtoBuf;

namespace Abc.Zebus.Persistence.Messages
{
    [ProtoContract]
    public class RemoveMessageFromQueueCommand : ICommand
    {
        [ProtoMember(1)]
        public readonly PeerId PeerId;

        [ProtoMember(2)]
        public readonly MessageId MessageId;

        public RemoveMessageFromQueueCommand(PeerId peerId, MessageId messageId)
        {
            PeerId = peerId;
            MessageId = messageId;
        }

        [UsedImplicitly]
        private RemoveMessageFromQueueCommand()
        {
        }
    }
}
