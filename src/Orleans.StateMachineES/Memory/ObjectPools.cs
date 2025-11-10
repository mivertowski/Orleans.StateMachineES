using System.Buffers;

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
        () => [],
        list => list.Clear());

    /// <summary>
    /// Pool for Dictionary&lt;string, object&gt; instances used for metadata and properties.
    /// </summary>
    public static readonly ObjectPool<Dictionary<string, object>> StringObjectDictionaryPool = new(
        () => [],
        dict => dict.Clear());

    /// <summary>
    /// Pool for StringBuilder-like operations using List&lt;char&gt; for efficient string building.
    /// </summary>
    public static readonly ObjectPool<List<char>> CharListPool = new(
        () => [],
        list => list.Clear());

    /// <summary>
    /// Pool for HashSet&lt;string&gt; instances used for deduplication and set operations.
    /// </summary>
    public static readonly ObjectPool<HashSet<string>> StringHashSetPool = new(
        () => new HashSet<string>(StringComparer.Ordinal),
        set => set.Clear());
}
