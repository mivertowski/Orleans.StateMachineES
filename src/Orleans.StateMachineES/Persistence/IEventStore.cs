namespace Orleans.StateMachineES.Persistence;

/// <summary>
/// Interface for event store implementations that persist state machine events.
/// Provides a provider-agnostic abstraction for event storage.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
public interface IEventStore<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    /// <summary>
    /// Appends events to a stream.
    /// </summary>
    /// <param name="streamId">The unique identifier for the event stream.</param>
    /// <param name="events">The events to append.</param>
    /// <param name="expectedVersion">
    /// The expected current version of the stream for optimistic concurrency.
    /// Use -1 for any version, -2 for stream should not exist.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the append operation.</returns>
    Task<AppendResult> AppendEventsAsync(
        string streamId,
        IEnumerable<StoredEvent<TState, TTrigger>> events,
        long expectedVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads events from a stream.
    /// </summary>
    /// <param name="streamId">The unique identifier for the event stream.</param>
    /// <param name="fromVersion">The starting version (inclusive).</param>
    /// <param name="count">Maximum number of events to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The events in the requested range.</returns>
    Task<IReadOnlyList<StoredEvent<TState, TTrigger>>> ReadEventsAsync(
        string streamId,
        long fromVersion,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads all events from a stream.
    /// </summary>
    /// <param name="streamId">The unique identifier for the event stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All events in the stream.</returns>
    Task<IReadOnlyList<StoredEvent<TState, TTrigger>>> ReadAllEventsAsync(
        string streamId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads events from a stream in reverse order (newest first).
    /// </summary>
    /// <param name="streamId">The unique identifier for the event stream.</param>
    /// <param name="fromVersion">The starting version (inclusive, from the end).</param>
    /// <param name="count">Maximum number of events to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The events in reverse order.</returns>
    Task<IReadOnlyList<StoredEvent<TState, TTrigger>>> ReadEventsBackwardAsync(
        string streamId,
        long fromVersion,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current version of a stream.
    /// </summary>
    /// <param name="streamId">The unique identifier for the event stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current version, or -1 if the stream doesn't exist.</returns>
    Task<long> GetStreamVersionAsync(
        string streamId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a stream exists.
    /// </summary>
    /// <param name="streamId">The unique identifier for the event stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the stream exists, false otherwise.</returns>
    Task<bool> StreamExistsAsync(
        string streamId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a stream and all its events.
    /// </summary>
    /// <param name="streamId">The unique identifier for the event stream.</param>
    /// <param name="expectedVersion">Expected version for optimistic concurrency.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the stream was deleted, false if it didn't exist.</returns>
    Task<bool> DeleteStreamAsync(
        string streamId,
        long expectedVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total event count across all streams.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total number of events.</returns>
    Task<long> GetTotalEventCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to new events in a stream.
    /// </summary>
    /// <param name="streamId">The unique identifier for the event stream.</param>
    /// <param name="fromVersion">The version to start from.</param>
    /// <param name="onEvent">Callback for each event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A disposable subscription.</returns>
    Task<IAsyncDisposable> SubscribeAsync(
        string streamId,
        long fromVersion,
        Func<StoredEvent<TState, TTrigger>, Task> onEvent,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Non-generic interface for event store operations.
/// </summary>
public interface IEventStore
{
    /// <summary>
    /// Gets the name of this event store provider.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Checks if the event store is available and healthy.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if healthy, false otherwise.</returns>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about the event store.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Event store statistics.</returns>
    Task<EventStoreStats> GetStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about an event store.
/// </summary>
public class EventStoreStats
{
    /// <summary>
    /// Gets or sets the total number of streams.
    /// </summary>
    public long StreamCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of events.
    /// </summary>
    public long EventCount { get; set; }

    /// <summary>
    /// Gets or sets the total storage size in bytes.
    /// </summary>
    public long StorageSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the oldest event timestamp.
    /// </summary>
    public DateTime? OldestEventTimestamp { get; set; }

    /// <summary>
    /// Gets or sets the newest event timestamp.
    /// </summary>
    public DateTime? NewestEventTimestamp { get; set; }

    /// <summary>
    /// Gets or sets additional provider-specific statistics.
    /// </summary>
    public Dictionary<string, object>? ExtendedStats { get; set; }
}

/// <summary>
/// Expected version constants for append operations.
/// </summary>
public static class ExpectedVersion
{
    /// <summary>
    /// No version check - append regardless of current version.
    /// </summary>
    public const long Any = -1;

    /// <summary>
    /// Stream must not exist (first write).
    /// </summary>
    public const long NoStream = -2;

    /// <summary>
    /// Stream must exist (not first write).
    /// </summary>
    public const long StreamExists = -3;
}
