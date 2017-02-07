using System.Linq;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Serialization;
using log4net;

namespace Abc.Zebus.Persistence.Handlers
{
    public class PersistMessageCommandHandler : IMessageHandler<PersistMessageCommand>
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(PersistMessageCommandHandler));

        private readonly Serializer _serializer = new Serializer();
        private readonly IMessageReplayerRepository _messageReplayerRepository;
        private readonly IInMemoryMessageMatcher _inMemoryMessageMatcher;
        private readonly IPersistenceConfiguration _configuration;

        public PersistMessageCommandHandler(IMessageReplayerRepository messageReplayerRepository, IInMemoryMessageMatcher inMemoryMessageMatcher, IPersistenceConfiguration configuration)
        {
            _messageReplayerRepository = messageReplayerRepository;
            _inMemoryMessageMatcher = inMemoryMessageMatcher;
            _configuration = configuration;
        }

        public void Handle(PersistMessageCommand message)
        {
            if (message.Targets == null)
                return;

            var messageBytes = _serializer.Serialize(message.TransportMessage.GetContentBytes());
            foreach (var target in message.Targets)
            {
                if (_configuration.PeerIdsToInvestigate != null && _configuration.PeerIdsToInvestigate.Contains(target.ToString()))
                    _log.Info($"Message received for peer {target}. MessageId: {message.TransportMessage.Id}. MessageType: {message.TransportMessage.MessageTypeId}");

                _messageReplayerRepository.GetActiveMessageReplayer(target)?.AddLiveMessage(message.TransportMessage);
                _inMemoryMessageMatcher.EnqueueMessage(target, message.TransportMessage.Id, message.TransportMessage.MessageTypeId, messageBytes);
            }

        }
    }
}