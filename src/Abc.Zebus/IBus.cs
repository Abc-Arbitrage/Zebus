using System;
using System.Threading.Tasks;

namespace Abc.Zebus
{
    public interface IBus : IDisposable
    {
        PeerId PeerId { get; }
        string Environment { get; }
        bool IsRunning { get; }

        void Configure(PeerId peerId, string environment);

        void Publish(IEvent message);
        
        Task<CommandResult> Send(ICommand message);
        Task<CommandResult> Send(ICommand message, Peer peer);

        IDisposable Subscribe(Subscription subscription, SubscriptionOptions options = SubscriptionOptions.Default);
        IDisposable Subscribe(Subscription[] subscriptions, SubscriptionOptions options = SubscriptionOptions.Default);
        IDisposable Subscribe<T>(Action<T> handler) where T : class, IMessage;
        IDisposable Subscribe(Subscription[] subscriptions, Action<IMessage> handler);
        IDisposable Subscribe(Subscription subscription, Action<IMessage> handler);

        void Reply(int errorCode);
        void Reply(IMessage response);
        
        void Start();
        void Stop();

        event Action Starting;
        event Action Started;
        event Action Stopping;
        event Action Stopped;
    }
}
