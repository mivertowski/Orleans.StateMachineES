using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Orleans.StateMachineES.Memory;

namespace Orleans.StateMachineES.Extensions;

/// <summary>
/// A high-performance string interning pool for reducing memory usage and improving string comparison performance.
/// Uses a bounded cache to prevent unbounded memory growth while maintaining performance benefits.
/// </summary>
public sealed class StringInternPool
{
    private readonly ConcurrentDictionary<string, string> _pool;
    private readonly int _maxSize;
    private volatile int _currentSize;

    /// <summary>
    /// Gets the default instance of the string intern pool.
    /// </summary>
    public static StringInternPool Default { get; } = new StringInternPool(10000);

    /// <summary>
    /// Initializes a new instance of the StringInternPool class.
    /// </summary>
    /// <param name="maxSize">The maximum number of strings to cache.</param>
    public StringInternPool(int maxSize = 10000)
    {
        if (maxSize <= 0)
            throw new ArgumentException("Max size must be greater than zero.", nameof(maxSize));

        _maxSize = maxSize;
        _pool = new ConcurrentDictionary<string, string>(
            Environment.ProcessorCount * 2, 
            Math.Min(maxSize, 1000));
    }

    /// <summary>
    /// Interns a string, returning a cached instance if available.
    /// </summary>
    /// <param name="value">The string to intern.</param>
    /// <returns>The interned string instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string Intern(string? value)
    {
        if (value == null)
            return null!;

        if (value.Length == 0)
            return string.Empty;

        // For very short strings, interning might not be worth it
        if (value.Length <= 2)
            return value;

        // Try to get from pool first
        if (_pool.TryGetValue(value, out var interned))
            return interned;

        // Check if we should add to pool
        if (_currentSize >= _maxSize)
        {
            // Pool is full, return original string
            return value;
        }

        // Try to add to pool
        var added = _pool.TryAdd(value, value);
        if (added)
        {
            System.Threading.Interlocked.Increment(ref _currentSize);
        }

        return value;
    }

    /// <summary>
    /// Interns a string for use as a state name, with optimizations for common state patterns.
    /// </summary>
    /// <param name="stateName">The state name to intern.</param>
    /// <returns>The interned state name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string InternState(string? stateName)
    {
        if (stateName == null)
            return null!;

        // Common state names that should always be interned
        return stateName switch
        {
            "Initial" => "Initial",
            "Active" => "Active",
            "Inactive" => "Inactive",
            "Completed" => "Completed",
            "Failed" => "Failed",
            "Pending" => "Pending",
            "Processing" => "Processing",
            "Suspended" => "Suspended",
            "Terminated" => "Terminated",
            _ => Intern(stateName)
        };
    }

    /// <summary>
    /// Interns a string for use as a trigger name, with optimizations for common trigger patterns.
    /// </summary>
    /// <param name="triggerName">The trigger name to intern.</param>
    /// <returns>The interned trigger name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string InternTrigger(string? triggerName)
    {
        if (triggerName == null)
            return null!;

        // Common trigger names that should always be interned
        return triggerName switch
        {
            "Start" => "Start",
            "Stop" => "Stop",
            "Pause" => "Pause",
            "Resume" => "Resume",
            "Complete" => "Complete",
            "Cancel" => "Cancel",
            "Retry" => "Retry",
            "Timeout" => "Timeout",
            "Error" => "Error",
            "Submit" => "Submit",
            "Approve" => "Approve",
            "Reject" => "Reject",
            _ => Intern(triggerName)
        };
    }

    /// <summary>
    /// Clears the intern pool, releasing all cached strings.
    /// </summary>
    public void Clear()
    {
        _pool.Clear();
        _currentSize = 0;
    }

    /// <summary>
    /// Gets the current number of interned strings.
    /// </summary>
    public int Count => _currentSize;

    /// <summary>
    /// Gets the maximum capacity of the intern pool.
    /// </summary>
    public int Capacity => _maxSize;

    /// <summary>
    /// Gets statistics about the intern pool usage.
    /// </summary>
    /// <returns>Pool statistics.</returns>
    public StringInternPoolStats GetStats()
    {
        return new StringInternPoolStats
        {
            CurrentSize = _currentSize,
            MaxSize = _maxSize,
            UtilizationPercent = (_currentSize * 100.0) / _maxSize
        };
    }
}

/// <summary>
/// Statistics for the string intern pool.
/// </summary>
public readonly struct StringInternPoolStats
{
    /// <summary>
    /// Gets the current number of interned strings.
    /// </summary>
    public int CurrentSize { get; init; }

    /// <summary>
    /// Gets the maximum capacity of the pool.
    /// </summary>
    public int MaxSize { get; init; }

    /// <summary>
    /// Gets the utilization percentage of the pool.
    /// </summary>
    public double UtilizationPercent { get; init; }
}