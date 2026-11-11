using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Abc.Zebus.Util.Extensions;

internal static class ExtendIEnumerable
{
    [Pure]
    public static HashSet<T> ToHashSet<T>([InstantHandle] this IEnumerable<T> collection)
    {
        return new HashSet<T>(collection);
    }

    [Pure]
    public static HashSet<T> ToHashSet<T>([InstantHandle] this IEnumerable<T> collection, IEqualityComparer<T> comparer)
    {
        return new HashSet<T>(collection, comparer);
    }

    [Pure]
    public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T>? collection)
        => collection ?? Enumerable.Empty<T>();

    [Pure]
    public static IList<T> AsList<T>([InstantHandle] this IEnumerable<T> collection)
        => collection is IList<T> list ? list : collection.ToList();

    [Pure]
    public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
    {
        var seenKeys = new HashSet<TKey>();
        return source.Where(element => seenKeys.Add(keySelector(element)));
    }

    /// <summary>
    /// Performs the specified <see cref="Action{T}" /> against every element of <see cref="IEnumerable{T}" />
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="enumerable">Enumerable to extend</param>
    /// <param name="action">Action to perform</param>
    /// <exception cref="ArgumentNullException">When any parameter is null</exception>
    public static void ForEach<T>([InstantHandle] this IEnumerable<T> enumerable, [InstantHandle] Action<T> action)
    {
        if (enumerable == null) throw new ArgumentNullException(nameof(enumerable));
        if (action == null) throw new ArgumentNullException(nameof(action));

        foreach (var t in enumerable)
        {
            action(t);
        }
    }

    /// <summary>
    /// Hides the underlying implementation
    /// </summary>
    public static IEnumerable<T> AsReadOnlyEnumerable<T>(this IEnumerable<T> items)
    {
        foreach (var item in items)
            yield return item;
    }
}
