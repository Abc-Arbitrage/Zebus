using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Abc.Zebus.Util.Collections;

internal class ConcurrentSet<T> : ICollection<T>
    where T : notnull
{
    private readonly ConcurrentDictionary<T, object?> _items = new();

    public ConcurrentSet()
    {
    }

    public ConcurrentSet(IEnumerable<T> items)
        : this()
    {
        foreach (var item in items)
            Add(item);
    }

    public int Count => _items.Count;

    public bool IsReadOnly => false;

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
        return _items.TryRemove(item, out var value);
    }
}
