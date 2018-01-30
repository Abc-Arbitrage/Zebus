using System;
using Abc.Zebus.Persistence.CQL;
using FluentDate;

namespace Abc.Zebus.Persistence.Runner
{
    class CassandraAppSettingsConfiguration : ICqlPersistenceConfiguration
    {
        public CassandraAppSettingsConfiguration()
        {
            Hosts = AppSettings.Get("Cassandra.Hosts", "");
            KeySpace = AppSettings.Get("Cassandra.KeySpace", "");
            QueryTimeout = AppSettings.Get("Casasndray.QueryTimeout", 5.Seconds());
            LocalDataCenter = AppSettings.Get("Casasndray.LocalDataCenter", "");
            OldestMessagePerPeerCheckPeriod = AppSettings.Get("Casasndray.OldestMessagePerPeerCheckPeriod", 1.Minutes());
        }

        public string Hosts { get; }
        public string KeySpace { get; }
        public TimeSpan QueryTimeout { get; }
        public string LocalDataCenter { get; }
        public TimeSpan OldestMessagePerPeerCheckPeriod { get; set; }
    }
}