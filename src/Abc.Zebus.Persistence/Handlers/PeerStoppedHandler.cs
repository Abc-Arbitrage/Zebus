using Abc.Zebus.Directory;

namespace Abc.Zebus.Persistence.Handlers
{
    public class PeerStoppedHandler : IMessageHandler<PeerStopped>
    {
        private readonly IMessageReplayerRepository _messageReplayerRepository;

        public PeerStoppedHandler(IMessageReplayerRepository messageReplayerRepository)
        {
            _messageReplayerRepository = messageReplayerRepository;
        }

        public void Handle(PeerStopped message)
        {
            var messageReplayer = _messageReplayerRepository.GetActiveMessageReplayer(message.PeerId);
            messageReplayer?.Cancel();
        }
    }
}