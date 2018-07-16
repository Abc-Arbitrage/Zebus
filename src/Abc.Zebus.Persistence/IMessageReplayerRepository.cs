using System;

namespace Abc.Zebus.Persistence
{
    public interface IMessageReplayerRepository
    {
        bool HasActiveMessageReplayers();

        IMessageReplayer CreateMessageReplayer(Peer peer, Guid replayId);
        void DeactivateMessageReplayers();

        IMessageReplayer GetActiveMessageReplayer(PeerId peerId);
        void SetActiveMessageReplayer(PeerId peerId, IMessageReplayer messageReplayer);
    }
}