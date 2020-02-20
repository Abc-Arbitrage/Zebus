using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Abc.Zebus.Dispatch;
using Abc.Zebus.Util.Extensions;

namespace Abc.Zebus.Scan
{
    public static class DispatchQueueNameScanner
    {
        public const string DefaultQueueName = "DefaultQueue";
        private static readonly ConcurrentDictionary<Assembly, AssemblyCache> _assemblyCaches = new ConcurrentDictionary<Assembly, AssemblyCache>();
        private static readonly ConcurrentDictionary<Type, string> _queueNames = new ConcurrentDictionary<Type, string>();
        private static readonly Func<Type, string> _loadQueueNameFunc = LoadQueueName;

        public static string GetQueueName(Type type)
        {
            return _queueNames.GetOrAdd(type, _loadQueueNameFunc);
        }

        private static string LoadQueueName(Type type)
        {
            var queueNameAttribute = (DispatchQueueNameAttribute?)Attribute.GetCustomAttribute(type, typeof(DispatchQueueNameAttribute), true);
            if (queueNameAttribute != null)
                return queueNameAttribute.QueueName;

            return LoadQueueNameFromNamespace(type);
        }

        private static string LoadQueueNameFromNamespace(Type type)
        {
            var assemblyCache = _assemblyCaches.GetOrAdd(type.Assembly, x => new AssemblyCache(x));
            return assemblyCache.GetQueueNameFromNamespace(type.Namespace ?? string.Empty);
        }

        private class AssemblyCache
        {
            private readonly Dictionary<string, string> _knownQueueNames = new Dictionary<string, string>();
            private readonly ConcurrentDictionary<string, string> _namespaceQueueNames = new ConcurrentDictionary<string, string>();

            public AssemblyCache(Assembly assembly)
            {
                foreach (var queueNameProviderType in assembly.GetTypes().Where(x => !x.IsInterface && !x.IsAbstract && typeof(IProvideDispatchQueueNameForCurrentNamespace).IsAssignableFrom(x)))
                {
                    if (queueNameProviderType.Namespace == null)
                        continue;

                    var queueNameProvider = (IProvideDispatchQueueNameForCurrentNamespace)Activator.CreateInstance(queueNameProviderType)!;
                    _knownQueueNames[queueNameProviderType.Namespace] = queueNameProvider.QueueName;
                }
            }

            public string GetQueueNameFromNamespace(string namespaze)
            {
                return _namespaceQueueNames.GetOrAdd(namespaze, FindQueueNameFromNamespace);
            }

            private string FindQueueNameFromNamespace(string @namespace)
            {
                var queueName = _knownQueueNames.GetValueOrDefault(@namespace);
                if (queueName != null)
                    return queueName;

                var parentNamespace = @namespace.Qualifier();
                if (parentNamespace.Length == @namespace.Length)
                    return DefaultQueueName;

                return string.IsNullOrEmpty(parentNamespace) ? DefaultQueueName : GetQueueNameFromNamespace(parentNamespace);
            }
        }
    }
}
