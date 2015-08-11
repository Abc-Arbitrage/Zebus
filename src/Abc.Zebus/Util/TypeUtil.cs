using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Abc.Zebus.Util
{
    internal static class TypeUtil
    {
        private static readonly ConcurrentDictionary<string, Type> _typesByNames = new ConcurrentDictionary<string, Type>();

        public static Type Resolve(string typeName)
        {
            return _typesByNames.GetOrAdd(typeName, FindTypeByName);
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

            if (typeName.Contains("<"))
                return FindGenericTypeByName(typeName);
            
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
            var genericArguments = typeName.Substring(typeName.IndexOf("<", StringComparison.Ordinal)).Trim('<', '>').Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
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
    }
}