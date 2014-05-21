using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using log4net;

namespace Abc.Zebus.Util
{
    internal static class TypeUtil
    {
        private static readonly ILog _logger = LogManager.GetLogger(typeof(TypeUtil));
        private static readonly ConcurrentDictionary<string, Type> _typesByNames = new ConcurrentDictionary<string, Type>();

        public static Type Resolve(string typeName)
        {
            return _typesByNames.GetOrAdd(typeName, FindTypeByName);
        }

        public static void EnsureAbcAssembliesAreLoaded()
        {
            AssemblyLoader.EnsureAbcAssembliesAreLoaded();
        }

        private static Type FindTypeByName(string typeName)
        {
            try
            {
                var type = Type.GetType(typeName);
                if (type != null)
                    return type;
            }
            catch (FileLoadException)
            {
            }

            AssemblyLoader.EnsureAbcAssembliesAreLoaded();

            if (typeName.Contains("<"))
                return FindGenericTypeByName(typeName);
            
            // OrderBy(a => a.FullName) because 99% of requested types will be in Abc assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(a => a.FullName))
            {
                var type = assembly.GetType(typeName);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static Type FindGenericTypeByName(string typeName)
        {
            var genericArguments = typeName.Substring(typeName.IndexOf("<", System.StringComparison.Ordinal)).Trim('<', '>').Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var typeNameWithoutGenericArguments = typeName.Substring(0, typeName.IndexOf("<", StringComparison.Ordinal)) + '`' + genericArguments.Length;


            var type = Resolve(typeNameWithoutGenericArguments);
            if (type == null)
                return null;
            
            var genericTypes = new List<Type>();
            foreach (var genericArgument in genericArguments)
            {
                var genericType = Resolve(genericArgument);
                if (genericType == null)
                    return null;
                genericTypes.Add(genericType);
            }
            return type.MakeGenericType(genericTypes.ToArray());
        }

        private static class AssemblyLoader
        {
            static AssemblyLoader()
            {
                foreach (var assemblyPath in System.IO.Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "Abc.*.dll"))
                {
                    var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
                    try
                    {
                        Assembly.Load(assemblyName);
                    }
                    catch (Exception ex)
                    {
                        _logger.WarnFormat("Unable to load assembly {0}: {1}", assemblyName, ex);
                    }
                }
            }

            public static void EnsureAbcAssembliesAreLoaded()
            {
            }
        }
    }
}