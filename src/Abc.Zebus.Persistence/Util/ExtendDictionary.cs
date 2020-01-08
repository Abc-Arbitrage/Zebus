using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace Abc.Zebus.Persistence.Util
{
    internal static class ExtendDictionary
    {
        [Pure, CanBeNull]
        [return: MaybeNull]
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
            where TKey : notnull
        {
            return dictionary.TryGetValue(key, out var value) ? value : default;
        }
    }
}
