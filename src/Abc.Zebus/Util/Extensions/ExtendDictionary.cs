using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Abc.Zebus.Util.Annotations;

namespace Abc.Zebus.Util.Extensions
{
    internal static class ExtendDictionary
    {
        public static TValue GetValueOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> valueBuilder)
        {
            return dictionary.GetValueOrAdd(key, _ => valueBuilder());
        }

        public static TValue GetValueOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> valueBuilder)
        {
            TValue value;
            if (!dictionary.TryGetValue(key, out value))
            {
                value = valueBuilder(key);
                dictionary.Add(key, value);
            }
            return value;
        }

        [Pure]
        public static TValue? GetValueOrNull<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key) where TValue : struct
        {
            TValue value;
            return dictionary.TryGetValue(key, out value) ? value : (TValue?)null;
        }

        [Pure]
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        {
            return GetValueOrDefault(dictionary, key, default(TValue));
        }

        [Pure]
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue)
        {
            TValue value;
            return dictionary.TryGetValue(key, out value) ? value : defaultValue;
        }

        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, [InstantHandle] Func<TKey, TValue> defaultValueBuilder)
        {
            TValue value;
            return dictionary.TryGetValue(key, out value) ? value : defaultValueBuilder(key);
        }

        public static bool Remove<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key)
        {
            TValue value;
            return dictionary.TryRemove(key, out value);
        }

        public static bool TryRemove<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key, TValue comparisonValue)
        {
            return ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).Remove(new KeyValuePair<TKey, TValue>(key, comparisonValue));
        }

        public static void RemoveRange<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, IEnumerable<TKey> keys)
        {
            foreach (var key in keys)
            {
                dictionary.Remove(key);
            }
        }
    }
}
