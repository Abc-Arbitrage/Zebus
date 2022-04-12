using System.Linq;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Persistence.Storage;
using Abc.Zebus.Transport;
using Microsoft.Extensions.Logging;

namespace Abc.Zebus.Persistence.Handlers
{
    public class PersistMessageCommandHandler : IMessageHandler<PersistMessageCommand>
    {
        private static readonly ILogger _log = ZebusLogManager.GetLogger(typeof(PersistMessageCommandHandler));

        private readonly TransportMessageSerializer _serializer = new TransportMessageSerializer(50 * 1024);
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
            if (string.IsNullOrEmpty(transportMessage.MessageTypeId.FullName))
            {
                _log.LogError($"Message received with empty TypeId, MessageId: {transportMessage.Id}, SenderId: {transportMessage.Originator.SenderId}");
                return;
            }

            var transportMessageBytes = _serializer.Serialize(transportMessage);
            foreach (var target in message.Targets)
            {
                if (string.IsNullOrEmpty(target.ToString()))
                {
                    _log.LogError($"Message received with empty target, MessageId: {transportMessage.Id}, SenderId: {transportMessage.Originator.SenderId}");
                    continue;
                }

                if (_configuration.PeerIdsToInvestigate != null && _configuration.PeerIdsToInvestigate.Contains(target.ToString()))
                    _log.LogInformation($"Message received for peer {target}, MessageId: {transportMessage.Id}, MessageType: {transportMessage.MessageTypeId}");

                _messageReplayerRepository.GetActiveMessageReplayer(target)?.AddLiveMessage(transportMessage);
                _inMemoryMessageMatcher.EnqueueMessage(target, transportMessage.Id, transportMessage.MessageTypeId, transportMessageBytes);
            }
        }
    }
}
