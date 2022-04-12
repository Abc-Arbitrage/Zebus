using System.Linq;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Persistence.Messages;
using Microsoft.Extensions.Logging;

namespace Abc.Zebus.Persistence.Handlers
{
    public class MessageHandledHandler : IMessageHandler<MessageHandled>, IMessageHandler<RemoveMessageFromQueueCommand>, IMessageContextAware
    {
        private static readonly ILogger _log = ZebusLogManager.GetLogger(typeof(MessageHandledHandler));

        private readonly IMessageReplayerRepository _messageReplayerRepository;
        private readonly IInMemoryMessageMatcher _inMemoryMessageMatcher;
        private readonly IPersistenceConfiguration _configuration;

        public MessageHandledHandler(IMessageReplayerRepository messageReplayerRepository, IInMemoryMessageMatcher inMemoryMessageMatcher, IPersistenceConfiguration configuration)
        {
            _messageReplayerRepository = messageReplayerRepository;
            _inMemoryMessageMatcher = inMemoryMessageMatcher;
            _configuration = configuration;
        }

        public MessageContext? Context { get; set; }

        public void Handle(MessageHandled message)
        {
            AckMessage(Context!.SenderId, message.MessageId);
        }

        public void Handle(RemoveMessageFromQueueCommand message)
        {
            AckMessage(message.PeerId, message.MessageId);
        }

        private void AckMessage(PeerId peerId, MessageId messageId)
        {
            if (_configuration.PeerIdsToInvestigate != null && _configuration.PeerIdsToInvestigate.Contains(peerId.ToString()))
                _log.LogInformation($"Ack received from peer {peerId}. MessageId: {messageId}");

            _inMemoryMessageMatcher.EnqueueAck(peerId, messageId);

            var activeMessageReplayer = _messageReplayerRepository.GetActiveMessageReplayer(peerId);
            activeMessageReplayer?.OnMessageAcked(messageId);
        }
    }
}
