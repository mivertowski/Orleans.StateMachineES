using System.Collections.Frozen;
using System.Runtime.CompilerServices;

namespace Orleans.StateMachineES.Memory;

/// <summary>
/// Factory for creating frozen collections optimized for read-heavy scenarios.
/// FrozenDictionary and FrozenSet provide superior lookup performance for static data.
/// </summary>
public static class FrozenCollections
{
    /// <summary>
    /// Creates a FrozenDictionary from a regular dictionary for optimal read performance.
    /// Use this for lookup tables that won't change after initialization.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <param name="source">The source dictionary.</param>
    /// <param name="keyComparer">Optional key comparer for custom comparison logic.</param>
    /// <returns>A frozen dictionary optimized for lookups.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FrozenDictionary<TKey, TValue> ToFrozenDictionary<TKey, TValue>(
        this IEnumerable<KeyValuePair<TKey, TValue>> source,
        IEqualityComparer<TKey>? keyComparer = null)
        where TKey : notnull
    {
        return source.ToFrozenDictionary(keyComparer);
    }

    /// <summary>
    /// Creates a FrozenDictionary from key-value pairs using a selector function.
    /// </summary>
    /// <typeparam name="TSource">The type of source elements.</typeparam>
    /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <param name="source">The source enumerable.</param>
    /// <param name="keySelector">Function to extract keys from source elements.</param>
    /// <param name="valueSelector">Function to extract values from source elements.</param>
    /// <param name="keyComparer">Optional key comparer.</param>
    /// <returns>A frozen dictionary.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FrozenDictionary<TKey, TValue> ToFrozenDictionary<TSource, TKey, TValue>(
        this IEnumerable<TSource> source,
        Func<TSource, TKey> keySelector,
        Func<TSource, TValue> valueSelector,
        IEqualityComparer<TKey>? keyComparer = null)
        where TKey : notnull
    {
        return source.ToFrozenDictionary(keySelector, valueSelector, keyComparer);
    }

    /// <summary>
    /// Creates a FrozenSet from a regular collection for optimal lookup performance.
    /// Use this for sets that won't change after initialization.
    /// </summary>
    /// <typeparam name="T">The type of elements in the set.</typeparam>
    /// <param name="source">The source collection.</param>
    /// <param name="equalityComparer">Optional equality comparer.</param>
    /// <returns>A frozen set optimized for lookups.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FrozenSet<T> ToFrozenSet<T>(
        this IEnumerable<T> source,
        IEqualityComparer<T>? equalityComparer = null)
    {
        return source.ToFrozenSet(equalityComparer);
    }
}

/// <summary>
/// Cached frozen collections for common state machine operations.
/// These are initialized once and reused for optimal performance.
/// </summary>
public static class StateMachineFrozenCollections
{
    /// <summary>
    /// Common state machine error messages frozen for fast lookup.
    /// </summary>
    public static readonly FrozenDictionary<string, string> CommonErrorMessages = new Dictionary<string, string>
    {
        ["INVALID_TRANSITION"] = "The specified trigger is not valid for the current state.",
        ["STATE_NOT_FOUND"] = "The specified state does not exist in the state machine configuration.",
        ["TRIGGER_NOT_FOUND"] = "The specified trigger is not configured for any state.",
        ["GUARD_CONDITION_FAILED"] = "The guard condition for this transition evaluated to false.",
        ["STATE_MACHINE_NOT_INITIALIZED"] = "The state machine has not been properly initialized.",
        ["CIRCULAR_DEPENDENCY"] = "A circular dependency was detected in the state machine configuration.",
        ["INVALID_STATE_HIERARCHY"] = "The state hierarchy configuration is invalid or incomplete.",
        ["CONCURRENT_MODIFICATION"] = "The state machine configuration was modified concurrently.",
        ["SERIALIZATION_ERROR"] = "An error occurred during state machine serialization or deserialization.",
        ["UNSUPPORTED_OPERATION"] = "This operation is not supported by the current state machine implementation."
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Common state names frozen for fast comparison and lookup.
    /// </summary>
    public static readonly FrozenSet<string> CommonStateNames = new HashSet<string>
    {
        "Initial", "Active", "Inactive", "Pending", "Processing", "Completed", "Failed", "Cancelled",
        "Draft", "Published", "Archived", "Deleted", "Suspended", "Running", "Stopped", "Waiting",
        "Online", "Offline", "Connected", "Disconnected", "Ready", "Busy", "Idle", "Error",
        "New", "Open", "Closed", "Approved", "Rejected", "Review", "InProgress", "Done"
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Common trigger names frozen for fast comparison and lookup.
    /// </summary>
    public static readonly FrozenSet<string> CommonTriggerNames = new HashSet<string>
    {
        "Start", "Stop", "Pause", "Resume", "Reset", "Initialize", "Finalize", "Complete", "Fail", "Cancel",
        "Activate", "Deactivate", "Enable", "Disable", "Connect", "Disconnect", "Open", "Close",
        "Submit", "Approve", "Reject", "Review", "Update", "Delete", "Create", "Modify",
        "Timeout", "Retry", "Skip", "Continue", "Abort", "Commit", "Rollback", "Validate"
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// HTTP status codes mapped to common state machine outcomes for web scenarios.
    /// </summary>
    public static readonly FrozenDictionary<int, string> HttpStatusToStateMapping = new Dictionary<int, string>
    {
        [200] = "Completed",
        [201] = "Created", 
        [202] = "Accepted",
        [204] = "Done",
        [400] = "Invalid",
        [401] = "Unauthorized",
        [403] = "Forbidden",
        [404] = "NotFound",
        [409] = "Conflict",
        [422] = "ValidationFailed",
        [429] = "RateLimited",
        [500] = "Error",
        [502] = "ServiceUnavailable",
        [503] = "Unavailable",
        [504] = "Timeout"
    }.ToFrozenDictionary();

    /// <summary>
    /// Priority levels for state machine operations frozen for fast lookup.
    /// </summary>
    public static readonly FrozenDictionary<string, int> PriorityLevels = new Dictionary<string, int>
    {
        ["Critical"] = 0,
        ["High"] = 1,
        ["Normal"] = 2,
        ["Low"] = 3,
        ["Background"] = 4
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Builder pattern for creating optimized frozen collections from dynamic data.
/// Useful when you need to build collections once and use them repeatedly.
/// </summary>
/// <typeparam name="TKey">The type of keys.</typeparam>
/// <typeparam name="TValue">The type of values.</typeparam>
/// <remarks>
/// Initializes a new FrozenDictionaryBuilder.
/// </remarks>
/// <param name="capacity">Initial capacity hint.</param>
/// <param name="comparer">Key comparer.</param>
public sealed class FrozenDictionaryBuilder<TKey, TValue>(int capacity = 0, IEqualityComparer<TKey>? comparer = null)
    where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _items = capacity > 0 ? new Dictionary<TKey, TValue>(capacity) : [];
    private readonly IEqualityComparer<TKey>? _comparer = comparer;

    /// <summary>
    /// Adds a key-value pair to the builder.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public FrozenDictionaryBuilder<TKey, TValue> Add(TKey key, TValue value)
    {
        _items.Add(key, value);
        return this;
    }

    /// <summary>
    /// Adds multiple key-value pairs to the builder.
    /// </summary>
    /// <param name="items">The items to add.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public FrozenDictionaryBuilder<TKey, TValue> AddRange(IEnumerable<KeyValuePair<TKey, TValue>> items)
    {
        foreach (var item in items)
        {
            _items.Add(item.Key, item.Value);
        }
        return this;
    }

    /// <summary>
    /// Builds the final FrozenDictionary.
    /// </summary>
    /// <returns>A FrozenDictionary containing all added items.</returns>
    public FrozenDictionary<TKey, TValue> Build()
    {
        return _items.ToFrozenDictionary(_comparer);
    }

    /// <summary>
    /// Gets the current number of items in the builder.
    /// </summary>
    public int Count => _items.Count;
}