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

        public TimeSpan PeerPingInterval { get; private set; }
        public TimeSpan TransientPeerPingTimeout { get; private set; }
        public TimeSpan PersistentPeerPingTimeout { get; private set; }
        public TimeSpan DebugPeerPingTimeout { get; private set; }
        public string[] BlacklistedMachines { get; private set; }
        public string[] WildcardsForPeersNotToDecommissionOnTimeout { get; private set; }
        public bool DisableDynamicSubscriptionsForDirectoryOutgoingMessages { get; private set; }
        public TimeSpan? MaxAllowedClockDifferenceWhenRegistering { get; }
    }
}
