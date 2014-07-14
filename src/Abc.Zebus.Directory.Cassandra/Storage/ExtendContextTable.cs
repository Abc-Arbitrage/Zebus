using Cassandra.Data.EntityContext;
using Cassandra.Data.Linq;

namespace Abc.Zebus.Directory.Cassandra.Storage
{
    // Is necessary because Insert from ContextTable hides Insert from Table (https://datastax-oss.atlassian.net/browse/CSHARP-137)
    public static class ExtendContextTable
    {
        public static CqlInsert<TEntity> CreateInsert<TEntity>(this ContextTable<TEntity> table, TEntity entity)
        {
            return ((Table<TEntity>)table).Insert(entity);
        }
    }
}