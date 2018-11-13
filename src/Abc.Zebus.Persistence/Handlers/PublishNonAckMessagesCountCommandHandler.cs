using System.Linq;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Persistence.Storage;

namespace Abc.Zebus.Persistence.Handlers
{
    public class PublishNonAckMessagesCountCommandHandler : IMessageHandler<PublishNonAckMessagesCountCommand>
    {
        private readonly IStorage _storage;
        private readonly IBus _bus;

        public PublishNonAckMessagesCountCommandHandler(IStorage storage, IBus bus)
        {
            _storage = storage;
            _bus = bus;
        }

        public void Handle(PublishNonAckMessagesCountCommand message)
        {
            var messagesCount = _storage.GetNonAckedMessageCountsForUpdatedPeers()
                                        .Select(x => new NonAckMessage(x.Key.ToString(), x.Value))
                                        .ToArray();

            _bus.Publish(new NonAckMessagesCountChanged(messagesCount));
        }
    }
}
