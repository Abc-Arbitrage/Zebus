using Cassandra.Data.EntityContext;
using Cassandra.Data.Linq;

namespace Abc.Zebus.Directory.Cassandra.Cql
{
    public static class ExtendContextTable
    {
        // Was necessary because of an ambiguity between two Insert methods, see https://datastax-oss.atlassian.net/browse/CSHARP-137
        public static CqlInsert<TEntity> CreateInsert<TEntity>(this ContextTable<TEntity> table, TEntity entity)
        {
            return (table as Table<TEntity>).Insert(entity);
        }
    }
}