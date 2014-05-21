using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Abc.Zebus.Util.Collections
{
    internal class ConcurrentList<T> : IList<T>
    {
        private readonly object _syncRoot = new object();

        private readonly List<T> _tempList1;
        private readonly List<T> _tempList2;

        private List<T> _innerList;

        public ConcurrentList()
        {
            _innerList = _tempList1 = new List<T>();
            _tempList2 = new List<T>();
        }

        public ConcurrentList(int capacity)
        {
            _innerList = _tempList1 = new List<T>(capacity);
            _tempList2 = new List<T>(capacity);
        }

        public ConcurrentList(IEnumerable<T> collection)
        {
            _innerList = _tempList1 = new List<T>(collection);
            _tempList2 = new List<T>(_innerList.Capacity);
        }

        public List<T> ToList()
        {
            return _innerList.ToList();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _innerList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(T item)
        {
            MutateInnerList(x => x.Add(item));
        }

        public void Clear()
        {
            MutateInnerList(x => x.Clear());
        }

        public bool Contains(T item)
        {
            return _innerList.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _innerList.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            return MutateInnerList(x => x.Remove(item));
        }

        public int Count
        {
            get { return _innerList.Count; }
        }

        public bool IsReadOnly
        {
            get { return ((IList<T>)_innerList).IsReadOnly; }
        }

        public int IndexOf(T item)
        {
            return _innerList.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            MutateInnerList(x => x.Insert(index, item));
        }

        public void RemoveAt(int index)
        {
            MutateInnerList(x => x.RemoveAt(index));
        }

        public T this[int index]
        {
            get { return _innerList[index]; }
            set { MutateInnerList(x => x[index] = value); }
        }

        private TResult MutateInnerList<TResult>(Func<List<T>, TResult> func)
        {
            lock (_syncRoot)
            {
                var list = _innerList == _tempList1 ? _tempList2 : _tempList1;
                list.Clear();
                list.AddRange(_innerList);
                var result = func(list);
                _innerList = list;
                return result;
            }
        }

        private void MutateInnerList(Action<List<T>> action)
        {
            MutateInnerList(x =>
            {
                action(x);
                return (object)null;
            });
        }
    }
}