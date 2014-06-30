using System;

namespace Abc.Zebus.Directory.Configuration
{
    public interface IDirectoryConfiguration
    {
        TimeSpan PeerPingInterval { get; }
        TimeSpan TransientPeerPingTimeout { get; }
        TimeSpan PersistentPeerPingTimeout { get; }
        TimeSpan DebugPeerPingTimeout { get; }
        string[] BlacklistedMachines { get; }
        bool DisableDynamicSubscriptionsForDirectoryOutgoingMessages { get; }
    }
}