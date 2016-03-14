using System;

namespace Abc.Zebus.Persistence.Storage
{
    public class StorageTimeoutException : Exception
    {
        public StorageTimeoutException()
        {
        }

        public StorageTimeoutException(string message) : base(message)
        {
        }

        public StorageTimeoutException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}