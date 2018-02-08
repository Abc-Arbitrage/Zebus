using System;
using Abc.Zebus.Persistence.CQL;

namespace Abc.Zebus.Persistence.Runner
{
    class CassandraAppSettingsConfiguration : ICqlPersistenceConfiguration
    {
        public CassandraAppSettingsConfiguration()
        {
            Hosts = AppSettings.Get("Cassandra.Hosts", "");
            KeySpace = AppSettings.Get("Cassandra.KeySpace", "");
            QueryTimeout = AppSettings.Get("Casasndray.QueryTimeout", TimeSpan.FromSeconds(5));
            LocalDataCenter = AppSettings.Get("Casasndray.LocalDataCenter", "");
            OldestMessagePerPeerCheckPeriod = AppSettings.Get("Casasndray.OldestMessagePerPeerCheckPeriod", TimeSpan.FromMinutes(1));
        }

        public string Hosts { get; }
        public string KeySpace { get; }
        public TimeSpan QueryTimeout { get; }
        public string LocalDataCenter { get; }
        public TimeSpan OldestMessagePerPeerCheckPeriod { get; set; }
    }
}