using System.Collections.Generic;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.Storage;

namespace Abc.Zebus.Persistence.CQL.Storage
{
    public interface ICqlStorage : IStorage
    {
        Task UpdateNewOldestMessageTimestamp(PeerState peer);
        IEnumerable<PeerState> GetAllKnownPeers();
    }
}
