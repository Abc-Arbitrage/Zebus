using System.Linq;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Persistence.Storage;
using log4net;

namespace Abc.Zebus.Persistence.Handlers
{
    public class PersistMessageCommandHandler : IMessageHandler<PersistMessageCommand>
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(PersistMessageCommandHandler));

        private readonly TransportMessageSerializer _serializer = new TransportMessageSerializer();
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

            var transportMessage = message.TransportMessage;
            var transportMessageBytes = _serializer.Serialize(transportMessage);
            foreach (var target in message.Targets)
            {
                if (_configuration.PeerIdsToInvestigate != null && _configuration.PeerIdsToInvestigate.Contains(target.ToString()))
                    _log.Info($"Message received for peer {target}. MessageId: {transportMessage.Id}. MessageType: {transportMessage.MessageTypeId}");

                _messageReplayerRepository.GetActiveMessageReplayer(target)?.AddLiveMessage(transportMessage);
                _inMemoryMessageMatcher.EnqueueMessage(target, transportMessage.Id, transportMessage.MessageTypeId, transportMessageBytes);
            }
        }
    }
}