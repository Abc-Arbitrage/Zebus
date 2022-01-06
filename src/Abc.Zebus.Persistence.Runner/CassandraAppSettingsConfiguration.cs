using System;
using Abc.Zebus.Persistence.Cassandra;
using FluentDate;

namespace Abc.Zebus.Persistence.Runner
{
    class CassandraAppSettingsConfiguration : ICqlPersistenceConfiguration
    {
        public CassandraAppSettingsConfiguration()
        {
            Hosts = AppSettings.Get("Cassandra.Hosts", "");
            KeySpace = AppSettings.Get("Cassandra.KeySpace", "");
            QueryTimeout = AppSettings.Get("Cassandra.QueryTimeout", 5.Seconds());
            LocalDataCenter = AppSettings.Get("Cassandra.LocalDataCenter", "");
            OldestMessagePerPeerCheckPeriod = AppSettings.Get("Cassandra.OldestMessagePerPeerCheckPeriod", 1.Minutes());
            OldestMessagePerPeerGlobalCheckPeriod = AppSettings.Get("Cassandra.OldestMessagePerPeerCheckPeriod", 1.Hours());
        }

        public string Hosts { get; }
        public string KeySpace { get; }
        public TimeSpan QueryTimeout { get; }
        public string LocalDataCenter { get; }
        public TimeSpan OldestMessagePerPeerCheckPeriod { get; set; }
        public TimeSpan OldestMessagePerPeerGlobalCheckPeriod { get; set; }
    }
}
