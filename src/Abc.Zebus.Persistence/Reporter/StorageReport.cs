using System.Collections.Generic;

namespace Abc.Zebus.Persistence.Reporter
{
    public class StorageReport
    {
        public int MessageCount { get; }
        public int BatchSizeInBytes { get; }
        public int FattestMessageSizeInBytes { get; }
        public string FattestMessageTypeId { get; }
        public Dictionary<string, MessageTypeStorageReport> MessageTypeStorageReports { get; }

        public StorageReport(int messageCount, int batchSizeInBytes, int fattestMessageSizeInBytes, string fattestMessageTypeId, Dictionary<string, MessageTypeStorageReport> messageTypeStorageReports)
        {
            MessageCount = messageCount;
            BatchSizeInBytes = batchSizeInBytes;
            FattestMessageSizeInBytes = fattestMessageSizeInBytes;
            FattestMessageTypeId = fattestMessageTypeId;
            MessageTypeStorageReports = messageTypeStorageReports;
        }

        public override string ToString()
            => $"{nameof(MessageCount)}: {MessageCount}, {nameof(BatchSizeInBytes)}: {BatchSizeInBytes}, {nameof(FattestMessageSizeInBytes)}: {FattestMessageSizeInBytes}, {nameof(FattestMessageTypeId)}: {FattestMessageTypeId}, {nameof(MessageTypeStorageReports)}: {MessageTypeStorageReports}";
    }

    public record MessageTypeStorageReport(int Count, int TotalBytes);
}
