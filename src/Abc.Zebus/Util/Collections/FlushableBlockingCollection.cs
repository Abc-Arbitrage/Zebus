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
        private volatile ConcurrentQueue<T> _addQueue;
        private ConcurrentQueue<T> _nextQueue;
        private ManualResetEvent _nextQueueAcquiredSignal;
        private bool _isAddingCompleted;

        public FlushableBlockingCollection()
        {
            _addQueue = _queue;
        }

        public int Count
        {
            get { return _queue.Count; }
        }

        public void CompleteAdding()
        {
            _isAddingCompleted = true;
            _addSignal.Set();
        }

        public void Add(T item)
        {
            if (_isAddingCompleted)
                throw new InvalidOperationException("Adding completed");

            _addQueue.Enqueue(item);
            _addSignal.Set();
        }

        public IEnumerable<T> GetConsumingEnumerable()
        {
            while (true)
            {
                do
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
                while (_nextQueue == null && (!_isAddingCompleted || _queue.Count != 0));

                if (_nextQueue == null)
                    yield break;

                _queue = _nextQueue;
                _nextQueue = null;
                _nextQueueAcquiredSignal.Set();
            }
        }

        public void Dispose()
        {
            CompleteAdding();

            if (_nextQueueAcquiredSignal != null)
                _nextQueueAcquiredSignal.Dispose();
        }

        /// <summary>
        /// Known race condition: an item can be added to a flushed queue.
        /// </summary>
        public ConcurrentQueue<T> Flush(bool waitForCompletion = false)
        {
            if (_nextQueueAcquiredSignal != null && !_nextQueueAcquiredSignal.WaitOne(1.Minute()))
                throw new TimeoutException("Unable to flush collection, previous flush has not yet been completed");

            if (_nextQueueAcquiredSignal == null)
                _nextQueueAcquiredSignal = new ManualResetEvent(false);
            else
                _nextQueueAcquiredSignal.Reset();

            var items = _queue;

            _addQueue = new ConcurrentQueue<T>();
            _nextQueue = _addQueue;
            _addSignal.Set();

            if (waitForCompletion)
                _nextQueueAcquiredSignal.WaitOne(5.Minute());

            return items;
        }
    }
}