using System;
using System.Threading;
using Abc.Zebus.Util;

namespace Abc.Zebus.Persistence.Matching
{
    public class MatcherEntry
    {
        private MatcherEntry(PeerId peerId, MessageId messageId, string messageTypeName, byte[]? messageBytes, MatcherEntryType type, EventWaitHandle? waitHandle)
        {
            PeerId = peerId;
            MessageId = messageId;
            MessageTypeName = messageTypeName;
            MessageBytes = messageBytes;
            TimestampUtc = SystemDateTime.UtcNow;
            Type = type;
            WaitHandle = waitHandle;
        }

        public PeerId PeerId { get; }
        public MessageId MessageId { get; }
        public string MessageTypeName { get; }
        public byte[]? MessageBytes { get; }
        public EventWaitHandle? WaitHandle { get; }
        public DateTime TimestampUtc { get; }
        public MatcherEntryType Type { get; }

        public bool IsAck => Type == MatcherEntryType.Ack;
        public bool IsEventWaitHandle => Type == MatcherEntryType.EventWaitHandle;

        public static MatcherEntry Message(PeerId peerId, MessageId messageId, MessageTypeId messageTypeId, byte[] bytes)
        {
            return new MatcherEntry(peerId, messageId, messageTypeId.FullName!, bytes, MatcherEntryType.Message, null);
        }

        public static MatcherEntry Ack(PeerId peerId, MessageId messageId)
        {
            return new MatcherEntry(peerId, messageId, string.Empty, null, MatcherEntryType.Ack, null);
        }

        public static MatcherEntry EventWaitHandle(EventWaitHandle waitHandle)
        {
            return new MatcherEntry(default, default, string.Empty, null, MatcherEntryType.EventWaitHandle, waitHandle);
        }

        public bool CanBeProcessed(TimeSpan delay)
        {
            return IsEventWaitHandle || SystemDateTime.UtcNow - TimestampUtc >= delay;
        }

        public int MessageLength => MessageBytes?.Length ?? 0;
    }
}
