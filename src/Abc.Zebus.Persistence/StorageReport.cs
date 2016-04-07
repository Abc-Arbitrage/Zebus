namespace Abc.Zebus.Persistence
{
    public class StorageReport
    {
        public int MessageCount { get; }
        public int BatchSizeInBytes { get; }
        public int FattestMessageSizeInBytes { get; }
        public string FattestMessageTypeId { get; }

        public StorageReport(int messageCount, int batchSizeInBytes, int fattestMessageSizeInBytes, string fattestMessageTypeId)
        {
            MessageCount = messageCount;
            BatchSizeInBytes = batchSizeInBytes;
            FattestMessageSizeInBytes = fattestMessageSizeInBytes;
            FattestMessageTypeId = fattestMessageTypeId;
        }
    }
}