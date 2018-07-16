using Abc.Zebus.Directory;
using Abc.Zebus.Persistence.Messages;

namespace Abc.Zebus.Persistence.Handlers
{
    public class PeerDecommissionedHandler : IMessageHandler<PeerDecommissioned>
    {
        private readonly IBus _bus;

        public PeerDecommissionedHandler(IBus bus)
        {
            _bus = bus;
        }

        public void Handle(PeerDecommissioned message)
        {
            _bus.Send(new PurgeMessageQueueCommand(message.PeerId.ToString()));
        }
    }
}