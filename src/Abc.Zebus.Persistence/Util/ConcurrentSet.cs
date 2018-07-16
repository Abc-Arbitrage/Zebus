using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Abc.Zebus.Persistence.Util
{
    internal class ConcurrentSet<T> : ICollection<T>
    {
        private readonly ConcurrentDictionary<T, object> _items;

        public ConcurrentSet()
        {
            _items = new ConcurrentDictionary<T, object>();
        }

        public ConcurrentSet(IEqualityComparer<T> comparer)
        {
            _items = new ConcurrentDictionary<T, object>(comparer);
        }

        public ConcurrentSet(IEnumerable<T> items)
            : this()
        {
            foreach (var item in items)
                Add(item);
        }

        public ConcurrentSet(IEnumerable<T> items, IEqualityComparer<T> comparer)
            : this(comparer)
        {
            foreach (var item in items)
                Add(item);
        }

        public int Count
        {
            get { return _items.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _items.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _items.Keys.GetEnumerator();
        }

        public bool Add(T item)
        {
            return _items.TryAdd(item, null);
        }

        void ICollection<T>.Add(T item)
        {
            Add(item);
        }

        public void Clear()
        {
            _items.Clear();
        }

        public bool Contains(T item)
        {
            return _items.ContainsKey(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _items.Keys.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            object value;
            return _items.TryRemove(item, out value);
        }
    }
}