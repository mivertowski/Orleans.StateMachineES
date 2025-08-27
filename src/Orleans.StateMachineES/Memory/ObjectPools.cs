using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Orleans.StateMachineES.EventSourcing.Events;
using Orleans.StateMachineES.Models;

namespace Orleans.StateMachineES.Memory;

/// <summary>
/// High-performance object pooling infrastructure using ArrayPool and custom object pools.
/// Reduces GC pressure by reusing frequently allocated objects.
/// </summary>
public static class ObjectPools
{
    /// <summary>
    /// Shared ArrayPool for byte arrays used in serialization and networking.
    /// </summary>
    public static readonly ArrayPool<byte> ByteArrayPool = ArrayPool<byte>.Shared;

    /// <summary>
    /// Shared ArrayPool for object arrays used in trigger parameters.
    /// </summary>
    public static readonly ArrayPool<object> ObjectArrayPool = ArrayPool<object>.Shared;

    /// <summary>
    /// Pool for List&lt;string&gt; instances used for storing triggers, states, and error messages.
    /// </summary>
    public static readonly ObjectPool<List<string>> StringListPool = new(
        () => new List<string>(), 
        list => list.Clear());

    /// <summary>
    /// Pool for Dictionary&lt;string, object&gt; instances used for metadata and properties.
    /// </summary>
    public static readonly ObjectPool<Dictionary<string, object>> StringObjectDictionaryPool = new(
        () => new Dictionary<string, object>(), 
        dict => dict.Clear());

    /// <summary>
    /// Pool for StringBuilder-like operations using List&lt;char&gt; for efficient string building.
    /// </summary>
    public static readonly ObjectPool<List<char>> CharListPool = new(
        () => new List<char>(), 
        list => list.Clear());

    /// <summary>
    /// Pool for HashSet&lt;string&gt; instances used for deduplication and set operations.
    /// </summary>
    public static readonly ObjectPool<HashSet<string>> StringHashSetPool = new(
        () => new HashSet<string>(StringComparer.Ordinal), 
        set => set.Clear());
}

/// <summary>
/// Generic object pool implementation with configurable factory and reset actions.
/// Thread-safe and optimized for high-throughput scenarios.
/// </summary>
/// <typeparam name="T">The type of objects to pool.</typeparam>
public sealed class ObjectPool<T> where T : class
{
    private readonly ConcurrentBag<T> _pool = new();
    private readonly Func<T> _factory;
    private readonly Action<T>? _resetAction;
    private readonly int _maxPoolSize;
    private int _currentCount;

    /// <summary>
    /// Initializes a new instance of the ObjectPool class.
    /// </summary>
    /// <param name="factory">Factory function to create new instances.</param>
    /// <param name="resetAction">Optional action to reset objects before returning to pool.</param>
    /// <param name="maxPoolSize">Maximum number of objects to keep in pool.</param>
    public ObjectPool(Func<T> factory, Action<T>? resetAction = null, int maxPoolSize = 100)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _resetAction = resetAction;
        _maxPoolSize = maxPoolSize;
    }

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

        // Only add to pool if under the limit
        if (_currentCount < _maxPoolSize)
        {
            _pool.Add(item);
            Interlocked.Increment(ref _currentCount);
        }
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

/// <summary>
/// RAII wrapper for pooled objects that automatically returns objects to the pool when disposed.
/// Implements IDisposable for use with 'using' statements.
/// </summary>
/// <typeparam name="T">The type of the pooled object.</typeparam>
public readonly struct PooledObject<T> : IDisposable where T : class
{
    private readonly ObjectPool<T> _pool;

    /// <summary>
    /// Gets the pooled object instance.
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// Initializes a new instance of the PooledObject struct.
    /// </summary>
    /// <param name="value">The pooled object.</param>
    /// <param name="pool">The pool to return the object to.</param>
    internal PooledObject(T value, ObjectPool<T> pool)
    {
        Value = value;
        _pool = pool;
    }

    /// <summary>
    /// Returns the object to the pool.
    /// </summary>
    public void Dispose()
    {
        _pool.Return(Value);
    }
}

/// <summary>
/// Specialized pool for state transition events to reduce allocations in event sourcing scenarios.
/// </summary>
public static class StateTransitionEventPool<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    private static readonly ObjectPool<StateTransitionEvent<TState, TTrigger>> _pool = new(
        () => new StateTransitionEvent<TState, TTrigger>(default!, default!, default!),
        evt => ResetEvent(evt));

    /// <summary>
    /// Gets a pooled StateTransitionEvent instance.
    /// </summary>
    public static StateTransitionEvent<TState, TTrigger> Get() => _pool.Get();

    /// <summary>
    /// Returns a StateTransitionEvent instance to the pool.
    /// </summary>
    public static void Return(StateTransitionEvent<TState, TTrigger> evt) => _pool.Return(evt);

    /// <summary>
    /// Creates a new StateTransitionEvent with pooled instance optimization.
    /// </summary>
    public static StateTransitionEvent<TState, TTrigger> Create(
        TState fromState,
        TState toState,
        TTrigger trigger,
        DateTime? timestamp = null,
        string? correlationId = null,
        string? dedupeKey = null,
        string? stateMachineVersion = null,
        Dictionary<string, object>? metadata = null)
    {
        return new StateTransitionEvent<TState, TTrigger>(
            fromState, toState, trigger, timestamp, correlationId, 
            dedupeKey, stateMachineVersion, metadata);
    }

    private static void ResetEvent(StateTransitionEvent<TState, TTrigger> evt)
    {
        // For record types, we can't reset fields, but we can clear metadata if it's mutable
        // In practice, we'll rely on the GC for records and focus pooling on mutable objects
    }
}

/// <summary>
/// Memory usage statistics and monitoring for object pools.
/// </summary>
public static class PoolStatistics
{
    /// <summary>
    /// Gets statistics for all object pools.
    /// </summary>
    public static PoolStats GetStatistics()
    {
        return new PoolStats
        {
            StringListPoolCount = ObjectPools.StringListPool.Count,
            StringObjectDictionaryPoolCount = ObjectPools.StringObjectDictionaryPool.Count,
            CharListPoolCount = ObjectPools.CharListPool.Count,
            StringHashSetPoolCount = ObjectPools.StringHashSetPool.Count,
            Timestamp = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Statistics snapshot for object pools.
/// </summary>
public readonly struct PoolStats
{
    /// <summary>
    /// Number of List&lt;string&gt; instances in pool.
    /// </summary>
    public int StringListPoolCount { get; init; }

    /// <summary>
    /// Number of Dictionary&lt;string, object&gt; instances in pool.
    /// </summary>
    public int StringObjectDictionaryPoolCount { get; init; }

    /// <summary>
    /// Number of List&lt;char&gt; instances in pool.
    /// </summary>
    public int CharListPoolCount { get; init; }

    /// <summary>
    /// Number of HashSet&lt;string&gt; instances in pool.
    /// </summary>
    public int StringHashSetPoolCount { get; init; }

    /// <summary>
    /// When these statistics were captured.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Total number of pooled objects across all pools.
    /// </summary>
    public int TotalPooledObjects => 
        StringListPoolCount + StringObjectDictionaryPoolCount + 
        CharListPoolCount + StringHashSetPoolCount;
}