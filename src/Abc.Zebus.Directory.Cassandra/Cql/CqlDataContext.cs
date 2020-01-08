using System;
using System.Collections.Generic;
using System.Reflection;
using Cassandra;
using Cassandra.Data.Linq;
using TableAttribute = Cassandra.Mapping.Attributes.TableAttribute;

namespace Abc.Zebus.Directory.Cassandra.Cql
{
    public abstract class CqlDataContext<TConfig> where TConfig : ICassandraConfiguration
    {
        public CqlDataContext(CassandraCqlSessionManager sessionManager, TConfig cassandraConfiguration)
            : this(CreateSession(sessionManager, cassandraConfiguration))
        {
        }

        private CqlDataContext(ISession session)
        {
            Session = session;
        }

        public ISession Session { get; }

        protected static ISession CreateSession(CassandraCqlSessionManager sessionManager, ICassandraConfiguration cassandraConfiguration)
        {
            return sessionManager.GetSession(cassandraConfiguration);
        }

        public void CreateTablesIfNotExist()
        {
            foreach (var propertyInfo in GetTableProperties(GetType()))
            {
                var table = propertyInfo.GetMethod.Invoke(this, Array.Empty<object>());
                table.GetType().GetMethod(nameof(Table<object>.CreateIfNotExists)).Invoke(table, Array.Empty<object>());
            }
        }

        public IEnumerable<string> GetTableNames()
        {
            foreach (var propertyInfo in GetTableProperties(GetType()))
            {
                var genericArguments = propertyInfo.PropertyType.GetGenericArguments();
                if (genericArguments.Length != 1)
                    continue;

                var tableType = genericArguments[0];

                var tableAttribute = tableType.GetCustomAttribute<TableAttribute>();
                yield return tableAttribute.Name;
            }
        }

        private static IEnumerable<PropertyInfo> GetTableProperties(Type type)
        {
            if (!typeof(CqlDataContext<TConfig>).IsAssignableFrom(type))
                throw new ArgumentException();

            var properties = type.GetProperties();
            foreach (var propertyInfo in properties)
            {
                if (!propertyInfo.PropertyType.IsGenericType)
                    continue;

                if (typeof(Table<>).IsAssignableFrom(propertyInfo.PropertyType.GetGenericTypeDefinition()))
                    yield return propertyInfo;
            }
        }
    }
}
