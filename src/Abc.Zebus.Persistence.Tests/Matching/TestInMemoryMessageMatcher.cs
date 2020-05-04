using System.Collections.Generic;
using System.Threading;
using Abc.Zebus.Persistence.Matching;

namespace Abc.Zebus.Persistence.Tests.Matching
{
    public class TestInMemoryMessageMatcher : IInMemoryMessageMatcher
    {
        public long CassandraInsertCount { get; }
        public long InMemoryAckCount { get; }
        public List<(PeerId peerId, MessageId messageId, MessageTypeId messageTypeId, byte[] transportMessageBytes)> Messages { get; } = new List<(PeerId peerId, MessageId messageId, MessageTypeId messageTypeId, byte[] transportMessageBytes)>();

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public void EnqueueMessage(PeerId peerId, MessageId messageId, MessageTypeId messageTypeId, byte[] transportMessageBytes)
        {
            Messages.Add((peerId, messageId, messageTypeId, transportMessageBytes));
        }

        public void EnqueueAck(PeerId peerId, MessageId messageId)
        {
        }

        public void EnqueueWaitHandle(EventWaitHandle waitHandle)
        {
        }
    }
}
