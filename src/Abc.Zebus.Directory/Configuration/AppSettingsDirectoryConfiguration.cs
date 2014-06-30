using System;
using Abc.Zebus.Util;

namespace Abc.Zebus.Directory.Configuration
{
    public class AppSettingsDirectoryConfiguration : IDirectoryConfiguration
    {
        public AppSettingsDirectoryConfiguration()
        {
            PeerPingInterval = AppSettings.Get("Directory.PingPeers.Interval", 1.Minute());
            TransientPeerPingTimeout = AppSettings.Get("Directory.TransientPeers.PingTimeout", 5.Minutes());
            PersistentPeerPingTimeout = AppSettings.Get("Directory.PersistentPeers.PingTimeout", 5.Minutes());
            DebugPeerPingTimeout = AppSettings.Get("Directory.DebugPeers.PingTimeout", 10.Minutes());
            BlacklistedMachines = AppSettings.GetArray("Directory.BlacklistedMachines");
            DisableDynamicSubscriptionsForDirectoryOutgoingMessages = AppSettings.Get("Directory.DisableDynamicSubscriptionsForDirectoryOutgoingMessages", false);
        }

        public TimeSpan PeerPingInterval { get; private set; }
        public TimeSpan TransientPeerPingTimeout { get; private set; }
        public TimeSpan PersistentPeerPingTimeout { get; private set; }
        public TimeSpan DebugPeerPingTimeout { get; private set; }
        public string[] BlacklistedMachines { get; private set; }
        public bool DisableDynamicSubscriptionsForDirectoryOutgoingMessages { get; private set; }
    }
}