using System;
using Abc.Zebus.Util;

namespace Abc.Zebus.Core;

public class BusConfiguration : IBusConfiguration
{
    public BusConfiguration(string directoryServiceEndPoint)
        : this(new[] { directoryServiceEndPoint })
    {
    }

    public BusConfiguration(string[] directoryServiceEndPoints)
    {
        DirectoryServiceEndPoints = directoryServiceEndPoints;
    }

    public string[] DirectoryServiceEndPoints { get; set; }
    public TimeSpan RegistrationTimeout { get; set; } = 30.Seconds();
    public TimeSpan FaultedDirectoryRetryDelay { get; set; } = 5.Minutes();
    public TimeSpan StartReplayTimeout { get; set; } = 30.Seconds();
    public bool IsPersistent { get; set; } = false;
    public bool IsDirectoryPickedRandomly { get; set; } = true;
    public bool IsErrorPublicationEnabled { get; set; } = false;
    public int MessagesBatchSize { get; set; } = 100;
}
