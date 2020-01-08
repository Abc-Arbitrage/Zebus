namespace Abc.Zebus.Persistence.Handlers
{
    public class StartMessageReplayCommandHandler : IMessageHandler<StartMessageReplayCommand>, IMessageContextAware
    {
        private readonly IMessageReplayerRepository _messageReplayerRepository;

        public StartMessageReplayCommandHandler(IMessageReplayerRepository messageReplayerRepository)
        {
            _messageReplayerRepository = messageReplayerRepository;
        }

        public MessageContext Context { get; set; } = default!;

        public void Handle(StartMessageReplayCommand message)
        {
            var peer = Context.GetSender();

            var currentMessageReplayer = _messageReplayerRepository.GetActiveMessageReplayer(peer.Id);
            currentMessageReplayer?.Cancel();

            var newMessageReplayer = _messageReplayerRepository.CreateMessageReplayer(peer, message.ReplayId);
            _messageReplayerRepository.SetActiveMessageReplayer(peer.Id, newMessageReplayer);

            newMessageReplayer.Start();
        }
    }
}
