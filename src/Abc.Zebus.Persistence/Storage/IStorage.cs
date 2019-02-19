using System.Collections.Generic;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.Matching;

namespace Abc.Zebus.Persistence.Storage
{
    /// <summary>
    /// All interactions with the underlying storage have to go through this class.
    /// This interface (and <see cref="IMessageReader"/>) are required to be implemented to support a new storage type.
    /// higher layers can apply their retry policy
    /// </summary>
    public interface IStorage
    {
        int PersistenceQueueSize { get; }

        /// <summary>
        /// Stores a batch of entries (messages or acks) into the storage
        /// </summary>
        /// <param name="entriesToPersist">Batch to store</param>
        Task Write(IList<MatcherEntry> entriesToPersist);

        /// <summary>
        /// Returns an <see cref="IMessageReader"/> that allows to get messages and acks by batches
        /// </summary>
        /// <param name="peerId">The PeerId of the Peer to get messages for</param>
        /// <returns>A Disposable <see cref="IMessageReader"/> that allows to get message batches for the given Peer</returns>
        IMessageReader CreateMessageReader(PeerId peerId);

        /// <summary>
        /// Remove the specified peer.
        /// </summary>
        Task RemovePeer(PeerId peerId);

        /// <summary>
        /// Returns the count of all non-acked messages for peers updated since the last call to this method
        /// </summary>
        /// <returns></returns>
        Dictionary<PeerId, int> GetNonAckedMessageCounts();

        void Start();

        void Stop();
    }
}
