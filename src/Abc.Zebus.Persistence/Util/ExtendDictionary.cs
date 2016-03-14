using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Abc.Zebus.Util.Annotations;

namespace Abc.Zebus.Persistence.Util
{
    public static class ExtendDictionary
    {
        public static void AddOrUpdate<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, [NotNull]TKey key, [NotNull]Func<TKey, TValue> addValueFactory, [NotNull]Func<TKey, TValue, TValue> updateValueFactory)
        {
            TValue value;
            if (dictionary.TryGetValue(key, out value))
            {
                dictionary[key] = updateValueFactory(key, value);
            }
            else
            {
                dictionary[key] = addValueFactory(key);
            }
        }

        [Pure, CanBeNull]
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        {
            return GetValueOrDefault(dictionary, key, default(TValue));
        }

        [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue)
        {
            TValue value;
            return dictionary.TryGetValue(key, out value) ? value : defaultValue;
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
    }
}