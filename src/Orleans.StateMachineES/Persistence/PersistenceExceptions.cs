namespace Orleans.StateMachineES.Persistence;

/// <summary>
/// Base exception for persistence-related errors.
/// </summary>
public class PersistenceException : Exception
{
    /// <summary>
    /// Gets the stream ID related to this exception.
    /// </summary>
    public string? StreamId { get; }

    /// <summary>
    /// Gets the provider name that threw this exception.
    /// </summary>
    public string? ProviderName { get; }

    /// <summary>
    /// Creates a new persistence exception.
    /// </summary>
    public PersistenceException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a new persistence exception with inner exception.
    /// </summary>
    public PersistenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates a new persistence exception with context.
    /// </summary>
    public PersistenceException(string message, string? streamId, string? providerName, Exception? innerException = null)
        : base(message, innerException)
    {
        StreamId = streamId;
        ProviderName = providerName;
    }
}

/// <summary>
/// Exception thrown when there is a concurrency conflict.
/// </summary>
public class ConcurrencyException : PersistenceException
{
    /// <summary>
    /// Gets the expected version.
    /// </summary>
    public long ExpectedVersion { get; }

    /// <summary>
    /// Gets the actual version found.
    /// </summary>
    public long ActualVersion { get; }

    /// <summary>
    /// Creates a new concurrency exception.
    /// </summary>
    public ConcurrencyException(string streamId, long expectedVersion, long actualVersion)
        : base($"Concurrency conflict for stream '{streamId}'. Expected version {expectedVersion}, but found {actualVersion}.")
    {
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }
}

/// <summary>
/// Exception thrown when a stream is not found.
/// </summary>
public class StreamNotFoundException : PersistenceException
{
    /// <summary>
    /// Creates a new stream not found exception.
    /// </summary>
    public StreamNotFoundException(string streamId)
        : base($"Stream '{streamId}' was not found.", streamId, null)
    {
    }
}

/// <summary>
/// Exception thrown when event storage fails.
/// </summary>
public class EventStoreException : PersistenceException
{
    /// <summary>
    /// Creates a new event store exception.
    /// </summary>
    public EventStoreException(string message, string? streamId = null, Exception? innerException = null)
        : base(message, streamId, null, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when snapshot storage fails.
/// </summary>
public class SnapshotStoreException : PersistenceException
{
    /// <summary>
    /// Creates a new snapshot store exception.
    /// </summary>
    public SnapshotStoreException(string message, string? streamId = null, Exception? innerException = null)
        : base(message, streamId, null, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a snapshot is corrupted or invalid.
/// </summary>
public class InvalidSnapshotException : SnapshotStoreException
{
    /// <summary>
    /// Gets the snapshot ID that was invalid.
    /// </summary>
    public string? SnapshotId { get; }

    /// <summary>
    /// Creates a new invalid snapshot exception.
    /// </summary>
    public InvalidSnapshotException(string streamId, string? snapshotId, string reason)
        : base($"Snapshot '{snapshotId}' for stream '{streamId}' is invalid: {reason}", streamId)
    {
        SnapshotId = snapshotId;
    }
}

/// <summary>
/// Exception thrown when checksum validation fails.
/// </summary>
public class ChecksumValidationException : PersistenceException
{
    /// <summary>
    /// Gets the expected checksum.
    /// </summary>
    public string? ExpectedChecksum { get; }

    /// <summary>
    /// Gets the actual checksum.
    /// </summary>
    public string? ActualChecksum { get; }

    /// <summary>
    /// Creates a new checksum validation exception.
    /// </summary>
    public ChecksumValidationException(string streamId, string? expectedChecksum, string? actualChecksum)
        : base($"Checksum validation failed for stream '{streamId}'. Expected '{expectedChecksum}', got '{actualChecksum}'.", streamId, null)
    {
        ExpectedChecksum = expectedChecksum;
        ActualChecksum = actualChecksum;
    }
}

/// <summary>
/// Exception thrown when persistence operation times out.
/// </summary>
public class PersistenceTimeoutException : PersistenceException
{
    /// <summary>
    /// Gets the operation that timed out.
    /// </summary>
    public string? Operation { get; }

    /// <summary>
    /// Gets the timeout duration.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Creates a new persistence timeout exception.
    /// </summary>
    public PersistenceTimeoutException(string operation, TimeSpan timeout, string? streamId = null)
        : base($"Operation '{operation}' timed out after {timeout.TotalSeconds:F1}s for stream '{streamId}'.", streamId, null)
    {
        Operation = operation;
        Timeout = timeout;
    }
}
