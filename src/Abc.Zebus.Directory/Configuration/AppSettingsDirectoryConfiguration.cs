using System;
using Abc.Zebus.Util;

namespace Abc.Zebus.Directory.Configuration
{
    public class AppSettingsDirectoryConfiguration : IDirectoryConfiguration
    {
        public AppSettingsDirectoryConfiguration()
            : this(new AppSettings())
        {
        }

        internal AppSettingsDirectoryConfiguration(AppSettings appSettings)
        {
            PeerPingInterval = appSettings.Get("Directory.PingPeers.Interval", 1.Minute());
            TransientPeerPingTimeout = appSettings.Get("Directory.TransientPeers.PingTimeout", 5.Minutes());
            PersistentPeerPingTimeout = appSettings.Get("Directory.PersistentPeers.PingTimeout", 5.Minutes());
            DebugPeerPingTimeout = appSettings.Get("Directory.DebugPeers.PingTimeout", 10.Minutes());
            BlacklistedMachines = appSettings.GetArray("Directory.BlacklistedMachines");
            DisableDynamicSubscriptionsForDirectoryOutgoingMessages = appSettings.Get("Directory.DisableDynamicSubscriptionsForDirectoryOutgoingMessages", false);
            WildcardsForPeersNotToDecommissionOnTimeout = new string[0];
            MaxAllowedClockDifferenceWhenRegistering = appSettings.Get<TimeSpan?>("Directory.MaxAllowedClockDifferenceWhenRegistering", null);
        }

        public TimeSpan PeerPingInterval { get; }
        public TimeSpan TransientPeerPingTimeout { get; }
        public TimeSpan PersistentPeerPingTimeout { get; }
        public TimeSpan DebugPeerPingTimeout { get; }
        public string[] BlacklistedMachines { get; }
        public string[] WildcardsForPeersNotToDecommissionOnTimeout { get; }
        public bool DisableDynamicSubscriptionsForDirectoryOutgoingMessages { get; }
        public TimeSpan? MaxAllowedClockDifferenceWhenRegistering { get; }
    }
}
