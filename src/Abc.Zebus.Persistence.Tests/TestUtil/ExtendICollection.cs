#region (c)2009 Lokad - New BSD license

// Copyright (c) Lokad 2009 
// Company: http://www.lokad.com
// This code is released under the terms of the new BSD licence

#endregion

using System;
using System.Collections.Generic;

namespace Abc.Zebus.Persistence.Tests.TestUtil
{
    /// <summary>
    /// Simple helper extensions for <see cref="ICollection{T}"/>
    /// </summary>
    public static class ExtendICollection
    {
        /// <summary>
        /// Adds all items to the target collection
        /// </summary>
        /// <typeparam name="T">type of the item within the collection</typeparam>
        /// <param name="collection">The collection</param>
        /// <param name="items">items to add to the collection</param>
        /// <returns>same collection instance</returns>
        public static ICollection<T> AddRange<T>(this ICollection<T> collection, IEnumerable<T> items)
        {
            if (collection == null) throw new ArgumentNullException("collection");
            if (items == null) throw new ArgumentNullException("items");

            var list = collection as List<T>;
            if (list != null)
            {
                list.AddRange(items);
                return list;
            }

            foreach (var item in items)
            {
                collection.Add(item);
            }
            return collection;
        }

        public static ICollection<T> AddRange<T>(this ICollection<T> collection, params T[] items)
        {
            return AddRange(collection, (IEnumerable<T>)items);
        }
    }
}