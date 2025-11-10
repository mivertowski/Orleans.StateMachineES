namespace Orleans.StateMachineES.Memory;

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
