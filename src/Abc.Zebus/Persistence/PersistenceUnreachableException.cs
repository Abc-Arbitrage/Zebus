using System;

namespace Abc.Zebus.Persistence
{
    public class PersistenceUnreachableException : Exception
    {
        public PersistenceUnreachableException(TimeSpan timeout, string[] directoryServiceEndPoints) 
            : base(string.Format("Zebus persistence did not retry before timeout ({0}). Directories: {1}", timeout, string.Join(", ", directoryServiceEndPoints)))
        {
        }
    }
}