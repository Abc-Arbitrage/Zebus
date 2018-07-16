using System.Collections.Generic;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.CQL.Storage;
using Abc.Zebus.Persistence.Messages;

namespace Abc.Zebus.Persistence.CQL
{
    public interface IPeerStateRepository : IEnumerable<PeerState>, IMessageHandler<PublishNonAckMessagesCountCommand>
    {
        void Initialize();

        PeerState GetPeerStateFor(PeerId peerId);

        void UpdateNonAckMessageCount(PeerId peerId, int delta);

        Task Purge(PeerId peerId);

        Task Save();
    }
}