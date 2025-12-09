using System;
using Abc.Zebus.Directory.Cassandra.Cql;
using Abc.Zebus.Util;

namespace Abc.Zebus.Directory.Runner
{
    class CassandraAppSettingsConfiguration : ICassandraConfiguration
    {
        public CassandraAppSettingsConfiguration()
        {
            Hosts = AppSettings.Get("Cassandra.Hosts", "");
            KeySpace = AppSettings.Get("Cassandra.KeySpace", "");
            QueryTimeout = AppSettings.Get("Cassandra.QueryTimeout", 5.Seconds());
            LocalDataCenter = AppSettings.Get("Cassandra.LocalDataCenter", "");
            UseSsl = AppSettings.Get("Cassandra.UseSsl", false);
        }

        public string Hosts { get; }
        public string KeySpace { get; }
        public TimeSpan QueryTimeout { get; }
        public string LocalDataCenter { get; }
        public bool UseSsl { get; }
    }
}
