using System.Collections.Generic;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.CQL.Storage;

namespace Abc.Zebus.Persistence.CQL
{
    public interface IPeerStateRepository : IEnumerable<PeerState>
    {
        void Initialize();

        PeerState GetPeerStateFor(PeerId peerId);

        void UpdateNonAckMessageCount(PeerId peerId, int delta);

        List<PeerState> GetUpdatedPeers(ref long version);

        Task Save();
        Task RemovePeer(PeerId peerId);
    }
}
