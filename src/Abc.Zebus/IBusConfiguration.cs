using System;

namespace Abc.Zebus
{
    public interface IBusConfiguration
    {
        string[] DirectoryServiceEndPoints { get; }

        TimeSpan RegistrationTimeout { get; }

        TimeSpan StartReplayTimeout { get; }

        bool IsPersistent { get; }
        
        bool IsDirectoryPickedRandomly { get; }
    }
}