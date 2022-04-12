using System;
using Abc.Zebus.Transport;

namespace Abc.Zebus.Persistence
{
    public interface IMessageReplayer
    {
        event Action Stopped;

        void AddLiveMessage(TransportMessage message);
        void Start();
        bool Cancel();
        void OnMessageAcked(MessageId messageId);
    }
}
