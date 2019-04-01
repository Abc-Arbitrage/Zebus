using System.Linq;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Persistence.Storage;

namespace Abc.Zebus.Persistence.Handlers
{
    public class PublishNonAckMessagesCountCommandHandler : IMessageHandler<PublishNonAckMessagesCountCommand>
    {
        private readonly IStorage _storage;
        private readonly IBus _bus;
        private readonly NonAckedCountCache _nonAckedCountCache = new NonAckedCountCache();

        public PublishNonAckMessagesCountCommandHandler(IStorage storage, IBus bus)
        {
            _storage = storage;
            _bus = bus;
        }

        public void Handle(PublishNonAckMessagesCountCommand message)
        {
            var allNonAckedCounts = _storage.GetNonAckedMessageCounts();
            var updatedNonAckedCounts = _nonAckedCountCache.GetUpdatedValues(allNonAckedCounts.Select(x => new NonAckedCount(x.Key, x.Value)));
            var messagesCount = updatedNonAckedCounts.Select(x => new NonAckMessage(x.PeerId.ToString(), x.Count))
                                                     .ToArray();

            _bus.Publish(new NonAckMessagesCountChanged(messagesCount));
        }
    }
}
