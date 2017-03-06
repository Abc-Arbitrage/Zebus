using System.Collections.Generic;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.Matching;

namespace Abc.Zebus.Persistence.Storage
{
    /// <summary>
    /// All interactions with the underlying storage have to go through this class.
    /// This interface (and <see cref="IMessageReader"/>) are required to be implemented to support a new storage type.
    /// Any timeout from the underlying storage should be signaled using a <see cref="StorageTimeoutException"/>, so that
    /// higher layers can apply their retry policy
    /// </summary>
    public interface IStorage
    {
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
        /// Purges the message queue for a given peer
        /// </summary>
        /// <param name="peerId">The concerned PeerId</param>
        void PurgeMessagesAndAcksForPeer(PeerId peerId);

        void Start();

        void Stop();

        int PersistenceQueueSize { get; }
    }
}