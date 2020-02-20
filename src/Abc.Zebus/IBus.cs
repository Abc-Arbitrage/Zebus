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

        Task<IDisposable> SubscribeAsync(SubscriptionRequest request);
        Task<IDisposable> SubscribeAsync(SubscriptionRequest request, Action<IMessage> handler);

        void Reply(int errorCode);
        void Reply(int errorCode, string? message);
        void Reply(IMessage? response);

        void Start();
        void Stop();

        event Action Starting;
        event Action Started;
        event Action Stopping;
        event Action Stopped;
    }
}
