using System.Linq;
using Abc.Zebus.Persistence.Matching;
using log4net;

namespace Abc.Zebus.Persistence.Handlers
{
    public class MessageHandledHandler : IMessageHandler<MessageHandled>, IMessageContextAware
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(MessageHandledHandler));

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
            if (_configuration.PeerIdsToInvestigate != null && _configuration.PeerIdsToInvestigate.Contains(Context!.SenderId.ToString()))
                _log.Info($"Ack received from peer {Context.SenderId}. MessageId: {message.MessageId}");

            _inMemoryMessageMatcher.EnqueueAck(Context!.SenderId, message.MessageId);

            var activeMessageReplayer = _messageReplayerRepository.GetActiveMessageReplayer(Context.SenderId);
            activeMessageReplayer?.Handle(message);
        }
    }
}
