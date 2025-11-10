using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Orleans.StateMachineES.Memory;

/// <summary>
/// Generic object pool implementation with configurable factory and reset actions.
/// Thread-safe and optimized for high-throughput scenarios.
/// </summary>
/// <typeparam name="T">The type of objects to pool.</typeparam>
/// <remarks>
/// Initializes a new instance of the ObjectPool class.
/// </remarks>
/// <param name="factory">Factory function to create new instances.</param>
/// <param name="resetAction">Optional action to reset objects before returning to pool.</param>
/// <param name="maxPoolSize">Maximum number of objects to keep in pool.</param>
public sealed class ObjectPool<T>(Func<T> factory, Action<T>? resetAction = null, int maxPoolSize = 100) where T : class
{
    private readonly ConcurrentBag<T> _pool = [];
    private readonly Func<T> _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    private readonly Action<T>? _resetAction = resetAction;
    private readonly int _maxPoolSize = maxPoolSize;
    private int _currentCount;

    /// <summary>
    /// Gets an object from the pool or creates a new one if none available.
    /// </summary>
    /// <returns>A pooled or new object instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Get()
    {
        return _pool.TryTake(out var item) ? item : _factory();
    }

    /// <summary>
    /// Returns an object to the pool for reuse.
    /// </summary>
    /// <param name="item">The object to return to the pool.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(T item)
    {
        if (item == null) return;

        // Apply reset action to clean up the object
        _resetAction?.Invoke(item);

        // Use CompareExchange loop to atomically check and increment count
        int currentCount, newCount;
        do
        {
            currentCount = _currentCount;
            if (currentCount >= _maxPoolSize)
            {
                // Pool is full, don't add the item
                return;
            }
            newCount = currentCount + 1;
        }
        while (Interlocked.CompareExchange(ref _currentCount, newCount, currentCount) != currentCount);

        // Successfully reserved a slot, add to pool
        _pool.Add(item);
    }

    /// <summary>
    /// Creates a disposable wrapper that automatically returns the object to the pool.
    /// </summary>
    /// <returns>A disposable wrapper containing a pooled object.</returns>
    public PooledObject<T> GetDisposable()
    {
        return new PooledObject<T>(Get(), this);
    }

    /// <summary>
    /// Gets the approximate number of objects currently in the pool.
    /// </summary>
    public int Count => _currentCount;
}
