using System;
using System.Threading;
using Abc.Zebus.Persistence.Util;

namespace Abc.Zebus.Persistence.Matching
{
    public class MatcherEntry
    {
        private MatcherEntry(PeerId peerId, MessageId messageId, string messageTypeName, byte[] messageBytes, bool isAck, EventWaitHandle waitHandle)
        {
            PeerId = peerId;
            MessageId = messageId;
            MessageTypeName = messageTypeName;
            MessageBytes = messageBytes;
            TimestampUtc = SystemDateTime.UtcNow;
            IsAck = isAck;
            WaitHandle = waitHandle;
        }

        public PeerId PeerId { get; }
        public MessageId MessageId { get; }
        public string MessageTypeName { get; }
        public byte[] MessageBytes { get; }
        public EventWaitHandle WaitHandle { get; }
        public DateTime TimestampUtc { get; }
        public bool IsAck { get; private set; }
        
        public bool IsEventWaitHandle => WaitHandle != null;

        public static MatcherEntry Message(PeerId peerId, MessageId messageId, MessageTypeId messageTypeId, byte[] bytes)
        {
            return new MatcherEntry(peerId, messageId, messageTypeId.FullName, bytes, false, null);
        }

        public static MatcherEntry Ack(PeerId peerId, MessageId messageId)
        {
            return new MatcherEntry(peerId, messageId, string.Empty, null, true, null);
        }

        public static MatcherEntry EventWaitHandle(EventWaitHandle waitHandle)
        {
            return new MatcherEntry(default(PeerId), default(MessageId), string.Empty, null, false, waitHandle);
        }
    }
}