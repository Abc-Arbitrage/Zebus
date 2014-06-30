using System;
using System.Configuration;

namespace Abc.Zebus.Directory.Configuration
{
    public class AppSettingsDirectoryConfiguration : IDirectoryConfiguration
    {
        public AppSettingsDirectoryConfiguration()
        {
            PeerPingInterval = TimeSpan.Parse(ConfigurationManager.AppSettings["Directory.PingPeers.Interval"]);
            TransientPeerPingTimeout = TimeSpan.Parse(ConfigurationManager.AppSettings["Directory.TransientPeers.PingTimeout"]);
            PersistentPeerPingTimeout = TimeSpan.Parse(ConfigurationManager.AppSettings["Directory.PersistentPeers.PingTimeout"]);
            DebugPeerPingTimeout = TimeSpan.Parse(ConfigurationManager.AppSettings["Directory.DebugPeers.PingTimeout"]);
            BlacklistedMachines = ConfigurationManager.AppSettings["Directory.BlacklistedMachines"].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            DisableDynamicSubscriptionsForDirectoryOutgoingMessages = Boolean.Parse(ConfigurationManager.AppSettings["Directory.DisableDynamicSubscriptionsForDirectoryOutgoingMessages"]);
        }

        public TimeSpan PeerPingInterval { get; private set; }
        public TimeSpan TransientPeerPingTimeout { get; private set; }
        public TimeSpan PersistentPeerPingTimeout { get; private set; }
        public TimeSpan DebugPeerPingTimeout { get; private set; }
        public string[] BlacklistedMachines { get; private set; }
        public bool DisableDynamicSubscriptionsForDirectoryOutgoingMessages { get; private set; }
    }
}