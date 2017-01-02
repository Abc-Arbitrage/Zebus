using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Abc.Zebus.Util.Collections
{
    internal class FlushableBlockingCollection<T> : IDisposable
    {
        private readonly ManualResetEventSlim _addSignal = new ManualResetEventSlim();
        private volatile ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();
        private bool _isAddingCompleted;

        public int Count => _queue.Count;

        public void CompleteAdding()
        {
            _isAddingCompleted = true;
            _addSignal.Set();
        }

        public void Add(T item)
        {
            if (_isAddingCompleted)
                throw new InvalidOperationException("Adding completed");

            _queue.Enqueue(item);
            _addSignal.Set();
        }

        public IEnumerable<T> GetConsumingEnumerable()
        {
            while (!IsAddingCompletedAndEmpty)
            {
                T item;
                if (_queue.TryDequeue(out item))
                {
                    yield return item;
                }
                else
                {
                    // a longer wait timeout decreases CPU usage and improves latency
                    // but the guy who wrote this code is not comfortable with long timeouts in waits or sleeps

                    if (_addSignal.Wait(200))
                        _addSignal.Reset();
                }
            }
        }

        private bool IsAddingCompletedAndEmpty => _isAddingCompleted && _queue.Count == 0;

        public void Dispose()
        {
            CompleteAdding();
        }

        /// <summary>
        /// Known race condition: an item can be added to a flushed queue.
        /// </summary>
        public ConcurrentQueue<T> Flush()
        {
            var items = _queue;

            _queue = new ConcurrentQueue<T>();
            _addSignal.Set();

            return items;
        }
    }
}