using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Persistence.Storage;

namespace Abc.Zebus.Persistence.Handlers
{
    public class PurgeMessageQueueCommandHandler : IMessageHandler<PurgeMessageQueueCommand>
    {
        private readonly IStorage _storage;

        public PurgeMessageQueueCommandHandler(IStorage storage)
        {
            _storage = storage;
        }

        public void Handle(PurgeMessageQueueCommand message)
        {
            _storage.PurgeMessagesAndAcksForPeer(new PeerId(message.InstanceName));
        }
    }
}