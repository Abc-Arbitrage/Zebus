using System;

namespace Abc.Zebus.Directory.Configuration
{
    public interface IDirectoryConfiguration
    {
        /// <summary>
        /// The interval at which the DeadPeerDetector pings Peers
        /// </summary>
        TimeSpan PeerPingInterval { get; }

        /// <summary>
        /// The amount of time after which a Transient Peer will be decommissionned
        /// if it fails to respond to ping commands
        /// </summary>
        TimeSpan TransientPeerPingTimeout { get; }

        /// <summary>
        /// The amount of time after which a Persistent Peer will be decommissionned
        /// if it fails to respond to ping commands
        /// </summary>
        TimeSpan PersistentPeerPingTimeout { get; }

        /// <summary>
        /// The amount of time after which a Peer attached to a debugger will be decommissionned
        /// if it fails to respond to ping commands (this prevents developers from being disconnected
        /// when debugging)
        /// </summary>
        TimeSpan DebugPeerPingTimeout { get; }

        /// <summary>
        /// The machine names of hosts that are not allowed to connect to this Directory
        /// (adding your CI server here might prevent bad surprises)
        /// </summary>
        string[] BlacklistedMachines { get; }

        /// <summary>
        /// Peers that should not be decommisionned on timeout, whether they are persistent or not
        /// </summary>
        string[] WildcardsForPeersNotToDecommissionOnTimeout { get; }

        /// <summary>
        /// USE WITH CAUTION
        /// This feature prevents the Peers from subscribing dynamically (https://github.com/Abc-Arbitrage/Zebus/wiki/Routing)
        /// to the events published by the Directory server.
        /// Its purpose is to dramatically improve the performance when working on a Bus
        /// with massive (> 50 000) amounts of dynamic subscriptions (which is not recommended anyway)
        /// </summary>
        bool DisableDynamicSubscriptionsForDirectoryOutgoingMessages { get; }

        /// <summary>
        /// Used to evaluate whether to reject register peer commands when the client's clock is ahead of the server's clock.
        /// </summary>
        TimeSpan? MaxAllowedClockDifferenceWhenRegistering { get; }
    }
}
