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

        public IDisposable Subscribe(Subscription subscription, SubscriptionOptions options = SubscriptionOptions.Default)
            => _bus.Subscribe(subscription);

        public IDisposable Subscribe(Subscription[] subscriptions, SubscriptionOptions options = SubscriptionOptions.Default)
            => _bus.Subscribe(subscriptions);

        public IDisposable Subscribe<T>(Action<T> handler) where T : class, IMessage
            => _bus.Subscribe(handler);

        public IDisposable Subscribe(Subscription[] subscriptions, Action<IMessage> handler)
            => _bus.Subscribe(subscriptions, handler);

        public IDisposable Subscribe(Subscription subscription, Action<IMessage> handler)
            => _bus.Subscribe(subscription, handler);

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
            add { _bus.Starting += value; }
            remove { _bus.Starting -= value; }
        }
        public event Action Started
        {
            add { _bus.Started += value; }
            remove { _bus.Started -= value; }
        }
        public event Action Stopping
        {
            add { _bus.Stopping += value; }
            remove { _bus.Stopping -= value; }
        }

        public event Action Stopped
        {
            add { _bus.Stopped += value; }
            remove { _bus.Stopped -= value; }
        }

        public void Dispose() => _bus.Dispose();
    }
}
