using System;
using Abc.Zebus.Persistence.CQL.Util;

namespace Abc.Zebus.Persistence.CQL
{
    public interface ICqlPersistenceConfiguration : ICassandraConfiguration
    {
        TimeSpan OldestMessagePerPeerCheckPeriod { get; set; }
    }
}