using System.Threading;

namespace Abc.Zebus.Persistence.Matching
{
    public interface IInMemoryMessageMatcher
    {
        long CassandraInsertCount { get; }
        long InMemoryAckCount { get; }

        void Start();
        void Stop();

        void EnqueueMessage(PeerId peerId, MessageId messageId, MessageTypeId messageTypeId, byte[] bytes);
        void EnqueueAck(PeerId peerId, MessageId messageId);
        void EnqueueWaitHandle(EventWaitHandle waitHandle);
    }
}