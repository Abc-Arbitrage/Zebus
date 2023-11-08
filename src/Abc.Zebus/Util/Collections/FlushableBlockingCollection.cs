using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Abc.Zebus.Util.Collections;

internal class FlushableBlockingCollection<T> : IDisposable
{
    private readonly ManualResetEventSlim _addSignal = new();
    private volatile ConcurrentQueue<T> _queue = new();
    private volatile bool _isAddingCompleted;
    private ManualResetEventSlim? _isEmptySignal;
    private int _hasChangedSinceLastWaitForEmpty;

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

        _hasChangedSinceLastWaitForEmpty = 1;
        _queue.Enqueue(item);
        _addSignal.Set();
    }

    public IEnumerable<List<T>> GetConsumingEnumerable(int maxSize)
    {
        var items = new List<T>(maxSize);
        while (!IsAddingCompletedAndEmpty)
        {
            if (_queue.TryDequeue(out var item))
            {
                _hasChangedSinceLastWaitForEmpty = 1;

                items.Clear();
                items.Add(item);

                while (items.Count < maxSize && _queue.TryDequeue(out item))
                    items.Add(item);

                yield return items;
            }
            else
            {
                _isEmptySignal?.Set();

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

    public bool WaitUntilIsEmpty()
    {
        var signal = _isEmptySignal;

        if (signal == null)
        {
            signal = new ManualResetEventSlim();
            var prevSignal = Interlocked.CompareExchange(ref _isEmptySignal, signal, null);
            signal = prevSignal ?? signal;
        }

        signal.Reset();
        _addSignal.Set();
        signal.Wait();

        return Interlocked.Exchange(ref _hasChangedSinceLastWaitForEmpty, 0) != 0;
    }
}
