using System;
using System.Threading.Tasks;

namespace Abc.Zebus.Core
{
    public class MessageContextAwareBus : IBus
    {
        private readonly IBus _bus;
        public readonly MessageContext MessageContext;

        public MessageContextAwareBus(IBus bus, MessageContext messageContext)
        {
            _bus = bus;
            MessageContext = messageContext;
        }

        public IBus InnerBus => _bus;
        public PeerId PeerId => _bus.PeerId;
        public string Environment => _bus.Environment;
        public bool IsRunning => _bus.IsRunning;

        public void Configure(PeerId peerId, string environment) => _bus.Configure(peerId, environment);

        public void Publish(IEvent message)
        {
            using (MessageContext.SetCurrent(MessageContext))
            {
                _bus.Publish(message);
            }
        }

        public Task<CommandResult> Send(ICommand message)
        {
            using (MessageContext.SetCurrent(MessageContext))
            {
                return _bus.Send(message);
            }
        }

        public Task<CommandResult> Send(ICommand message, Peer peer)
        {
            using (MessageContext.SetCurrent(MessageContext))
            {
                return _bus.Send(message, peer);
            }
        }

        public Task<IDisposable> SubscribeAsync(SubscriptionRequest request)
            => _bus.SubscribeAsync(request);

        public Task<IDisposable> SubscribeAsync(SubscriptionRequest request, Action<IMessage> handler)
            => _bus.SubscribeAsync(request, handler);

        public void Reply(int errorCode) => Reply(errorCode, null);

        public void Reply(int errorCode, string message)
        {
            MessageContext.ReplyCode = errorCode;
            MessageContext.ReplyMessage = message;
        }

        public void Reply(IMessage response)
        {
            MessageContext.ReplyResponse = response;
        }

        public void Start() => _bus.Start();
        public void Stop() => _bus.Stop();

        public event Action Starting
        {
            add => _bus.Starting += value;
            remove => _bus.Starting -= value;
        }

        public event Action Started
        {
            add => _bus.Started += value;
            remove => _bus.Started -= value;
        }

        public event Action Stopping
        {
            add => _bus.Stopping += value;
            remove => _bus.Stopping -= value;
        }

        public event Action Stopped
        {
            add => _bus.Stopped += value;
            remove => _bus.Stopped -= value;
        }

        public void Dispose() => _bus.Dispose();
    }
}
