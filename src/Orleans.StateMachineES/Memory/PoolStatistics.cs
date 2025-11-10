namespace Orleans.StateMachineES.Memory;

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
