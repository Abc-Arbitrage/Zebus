using System.Collections.Generic;
using JetBrains.Annotations;

namespace Abc.Zebus.Persistence.Util
{
    internal static class ExtendDictionary
    {
        [Pure, CanBeNull]
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        {
            TValue value;
            return dictionary.TryGetValue(key, out value) ? value : default(TValue);
        }
    }
}
