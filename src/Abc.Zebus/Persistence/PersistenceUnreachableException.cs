using System;

namespace Abc.Zebus.Persistence
{
    public class PersistenceUnreachableException : Exception
    {
        public PersistenceUnreachableException(TimeSpan timeout, string[] directoryServiceEndPoints) 
            : base($"Zebus persistence did not retry before timeout ({timeout}). Directories: {string.Join(", ", directoryServiceEndPoints)}")
        {
        }
    }
}