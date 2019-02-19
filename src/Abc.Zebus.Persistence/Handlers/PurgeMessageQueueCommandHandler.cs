using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Persistence.Storage;
using Abc.Zebus.Util;

namespace Abc.Zebus.Persistence.Handlers
{
    public class PurgeMessageQueueCommandHandler : IMessageHandler<PurgeMessageQueueCommand>
    {
        private readonly IStorage _storage;
        private readonly IBus _bus;

        public PurgeMessageQueueCommandHandler(IStorage storage, IBus bus)
        {
            _storage = storage;
            _bus = bus;
        }

        public void Handle(PurgeMessageQueueCommand message)
        {
            var peerId = new PeerId(message.InstanceName);
            _storage.RemovePeer(peerId).Wait(10.Seconds());

            _bus.Publish(new NonAckMessagesCountChanged(new[] { new NonAckMessage(peerId.ToString(), 0) }));
        }
    }
}
