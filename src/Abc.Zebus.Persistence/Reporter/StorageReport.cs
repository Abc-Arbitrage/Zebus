using System.Collections.Generic;

namespace Abc.Zebus.Persistence.Reporter
{
    public class StorageReport
    {
        public int MessageCount { get; }
        public int BatchSizeInBytes { get; }
        public int FattestMessageSizeInBytes { get; }
        public string FattestMessageTypeId { get; }
        public Dictionary<string, MessageTypeStatistics> MessageTypeStatistics { get; }

        public StorageReport(int messageCount, int batchSizeInBytes, int fattestMessageSizeInBytes, string fattestMessageTypeId, Dictionary<string, MessageTypeStatistics> messageTypeStatistics)
        {
            MessageCount = messageCount;
            BatchSizeInBytes = batchSizeInBytes;
            FattestMessageSizeInBytes = fattestMessageSizeInBytes;
            FattestMessageTypeId = fattestMessageTypeId;
            MessageTypeStatistics = messageTypeStatistics;
        }

        public override string ToString()
            => $"{nameof(MessageCount)}: {MessageCount}, {nameof(BatchSizeInBytes)}: {BatchSizeInBytes}, {nameof(FattestMessageSizeInBytes)}: {FattestMessageSizeInBytes}, {nameof(FattestMessageTypeId)}: {FattestMessageTypeId}, {nameof(MessageTypeStatistics)}: {MessageTypeStatistics}";
    }

    public record MessageTypeStatistics(int Count, int TotalBytes);
}
