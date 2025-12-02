namespace AnimeBingeDownloader.Models;

public class ThreadSafeBlockingPriorityQueue<T> : IDisposable
{
    private readonly object _lock = new();
    private readonly PriorityQueue<T, int> _pq = new();

    // Signal when at least one item is available
    private readonly ManualResetEventSlim _itemAvailable = new(false);
    // Signal to request shutdown
    private readonly ManualResetEventSlim _shutdownRequested = new(false);

    private bool _disposed;

    public void Enqueue(T item, int priority)
    {
        lock (_lock)
        {
            _pq.Enqueue(item, priority);
            // There is at least one item now
            _itemAvailable.Set();
        }
    }

    /// <summary>
    /// Dequeues the highest-priority item.
    /// Blocks until:
    ///   - An item exists, OR
    ///   - Shutdown is requested AND queue is empty (returns false).
    /// </summary>
    public bool TryDequeueBlocking(out T item, CancellationToken cancellationToken = default)
    {
        item = default!;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // First, try fast path: is there an item already?
            lock (_lock)
            {
                if (_pq.TryDequeue(out item!, out _))
                {
                    // If queue became empty, reset the item signal
                    if (_pq.Count == 0)
                        _itemAvailable.Reset();

                    return true;
                }

                // No item. If shutdown requested and queue is empty, we are done.
                if (_shutdownRequested.IsSet)
                {
                    item = default!;
                    return false;
                }
            }

            // Wait until either:
            //  - new item arrives, or
            //  - shutdown is requested, or
            //  - cancellation requested
            var signaled = WaitHandle.WaitAny(
                [_itemAvailable.WaitHandle, _shutdownRequested.WaitHandle],
                Timeout.Infinite,
                false
            );

            cancellationToken.ThrowIfCancellationRequested();

            // If shutdown was signaled, check again whether anything is left.
            if (signaled != 1 && !_shutdownRequested.IsSet) continue;
            lock (_lock)
            {
                if (_pq.Count != 0) continue;
                item = default!;
                return false;
                // if there *are* items left, just loop and dequeue them
            }

            // Otherwise loop back and try dequeue again.
        }
    }

    /// <summary>
    /// Ask all workers to shut down.
    /// They will exit once the queue is empty.
    /// </summary>
    public void RequestShutdown()
    {
        _shutdownRequested.Set();
        // Also wake any waiting threads so they can see the shutdown
        _itemAvailable.Set();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _itemAvailable.Dispose();
        _shutdownRequested.Dispose();
    }
}