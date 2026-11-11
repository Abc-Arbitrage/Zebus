using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace Abc.Zebus.Util.Extensions;

internal static class ExtendDictionary
{
    public static TValue GetValueOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> valueBuilder)
        where TKey : notnull
    {
        return dictionary.GetValueOrAdd(key, _ => valueBuilder());
    }

    public static TValue GetValueOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> valueBuilder)
        where TKey : notnull
    {
        if (!dictionary.TryGetValue(key, out var value))
        {
            value = valueBuilder(key);
            dictionary.Add(key, value);
        }

        return value;
    }

    [Pure]
    public static TValue? GetValueOrNull<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        where TKey : notnull
        where TValue : struct
    {
        return dictionary.TryGetValue(key, out var value) ? value : (TValue?)null;
    }

    [Pure]
    [return: MaybeNull]
    public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        where TKey : notnull
    {
        return GetValueOrDefault(dictionary, key, default(TValue)!);
    }

    [Pure]
    [return: MaybeNull]
    [return: NotNullIfNotNull("defaultValue")]
    public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue)
        where TKey : notnull
    {
        return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
    }

    public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, [InstantHandle] Func<TKey, TValue> defaultValueBuilder)
        where TKey : notnull
    {
        return dictionary.TryGetValue(key, out var value) ? value : defaultValueBuilder(key);
    }

    public static bool Remove<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key)
        where TKey : notnull
    {
        return dictionary.TryRemove(key, out _);
    }

    public static bool TryRemove<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key, TValue comparisonValue)
        where TKey : notnull
    {
        return ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).Remove(new KeyValuePair<TKey, TValue>(key, comparisonValue));
    }

    public static void RemoveRange<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, IEnumerable<TKey> keys)
        where TKey : notnull
    {
        foreach (var key in keys)
        {
            dictionary.Remove(key);
        }
    }

    public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
        where TKey : notnull
    {
        key = pair.Key;
        value = pair.Value;
    }
}
