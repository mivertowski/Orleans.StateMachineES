namespace Orleans.StateMachineES.Persistence;

/// <summary>
/// Combined interface for state machine persistence providing both event and snapshot storage.
/// Implementations can choose to provide either or both capabilities.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
public interface IStateMachinePersistence<TState, TTrigger> :
    IEventStore<TState, TTrigger>,
    ISnapshotStore<TState>,
    IEventStore,
    ISnapshotStore
    where TState : notnull
    where TTrigger : notnull
{
    /// <summary>
    /// Loads the state machine from storage, using snapshots when available.
    /// </summary>
    /// <param name="streamId">The unique identifier for the stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded state with event history.</returns>
    Task<LoadedState<TState, TTrigger>> LoadStateAsync(
        string streamId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the state machine at a specific point in time.
    /// </summary>
    /// <param name="streamId">The unique identifier for the stream.</param>
    /// <param name="pointInTime">The timestamp to load state at.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded state at the specified time.</returns>
    Task<LoadedState<TState, TTrigger>> LoadStateAtTimeAsync(
        string streamId,
        DateTime pointInTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the state machine at a specific version.
    /// </summary>
    /// <param name="streamId">The unique identifier for the stream.</param>
    /// <param name="version">The version to load.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded state at the specified version.</returns>
    Task<LoadedState<TState, TTrigger>> LoadStateAtVersionAsync(
        string streamId,
        long version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full history of state changes for a stream.
    /// </summary>
    /// <param name="streamId">The unique identifier for the stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete state history.</returns>
    Task<StateHistory<TState, TTrigger>> GetHistoryAsync(
        string streamId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paged history of state changes.
    /// </summary>
    /// <param name="streamId">The unique identifier for the stream.</param>
    /// <param name="page">The page number (0-based).</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The paged state history.</returns>
    Task<PagedHistory<TState, TTrigger>> GetHistoryPagedAsync(
        string streamId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compacts the event stream by creating a snapshot and optionally archiving old events.
    /// </summary>
    /// <param name="streamId">The unique identifier for the stream.</param>
    /// <param name="archiveOldEvents">Whether to archive events before the snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The compaction result.</returns>
    Task<CompactionResult> CompactStreamAsync(
        string streamId,
        bool archiveOldEvents = false,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a loaded state machine state with metadata.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
[GenerateSerializer]
[Alias("Orleans.StateMachineES.Persistence.LoadedState`2")]
public class LoadedState<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    /// <summary>
    /// Gets or sets the current state.
    /// </summary>
    [Id(0)]
    public TState? CurrentState { get; set; }

    /// <summary>
    /// Gets or sets the current version (event count).
    /// </summary>
    [Id(1)]
    public long Version { get; set; }

    /// <summary>
    /// Gets or sets whether the state was loaded from a snapshot.
    /// </summary>
    [Id(2)]
    public bool LoadedFromSnapshot { get; set; }

    /// <summary>
    /// Gets or sets the snapshot version if loaded from snapshot.
    /// </summary>
    [Id(3)]
    public long? SnapshotVersion { get; set; }

    /// <summary>
    /// Gets or sets the number of events replayed after the snapshot.
    /// </summary>
    [Id(4)]
    public int EventsReplayedCount { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last transition.
    /// </summary>
    [Id(5)]
    public DateTime? LastTransitionTimestamp { get; set; }

    /// <summary>
    /// Gets or sets whether the stream exists.
    /// </summary>
    [Id(6)]
    public bool StreamExists { get; set; }

    /// <summary>
    /// Gets or sets the events that were replayed.
    /// </summary>
    [Id(7)]
    public IReadOnlyList<StoredEvent<TState, TTrigger>>? ReplayedEvents { get; set; }

    /// <summary>
    /// Gets or sets the time taken to load the state.
    /// </summary>
    [Id(8)]
    public TimeSpan LoadDuration { get; set; }

    /// <summary>
    /// Creates a new loaded state indicating the stream doesn't exist.
    /// </summary>
    public static LoadedState<TState, TTrigger> NotFound()
    {
        return new LoadedState<TState, TTrigger>
        {
            StreamExists = false,
            Version = -1
        };
    }
}

/// <summary>
/// Represents the complete history of a state machine.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
[GenerateSerializer]
[Alias("Orleans.StateMachineES.Persistence.StateHistory`2")]
public class StateHistory<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    /// <summary>
    /// Gets or sets the stream identifier.
    /// </summary>
    [Id(0)]
    public string StreamId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current state.
    /// </summary>
    [Id(1)]
    public TState? CurrentState { get; set; }

    /// <summary>
    /// Gets or sets the initial state.
    /// </summary>
    [Id(2)]
    public TState? InitialState { get; set; }

    /// <summary>
    /// Gets or sets all events in chronological order.
    /// </summary>
    [Id(3)]
    public IReadOnlyList<StoredEvent<TState, TTrigger>> Events { get; set; } = [];

    /// <summary>
    /// Gets or sets the total number of transitions.
    /// </summary>
    [Id(4)]
    public int TransitionCount { get; set; }

    /// <summary>
    /// Gets or sets the first event timestamp.
    /// </summary>
    [Id(5)]
    public DateTime? FirstEventTimestamp { get; set; }

    /// <summary>
    /// Gets or sets the last event timestamp.
    /// </summary>
    [Id(6)]
    public DateTime? LastEventTimestamp { get; set; }

    /// <summary>
    /// Gets or sets the distinct states visited.
    /// </summary>
    [Id(7)]
    public IReadOnlyList<TState>? DistinctStates { get; set; }

    /// <summary>
    /// Gets or sets the distinct triggers used.
    /// </summary>
    [Id(8)]
    public IReadOnlyList<TTrigger>? DistinctTriggers { get; set; }
}

/// <summary>
/// Represents a paged subset of state history.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
[GenerateSerializer]
[Alias("Orleans.StateMachineES.Persistence.PagedHistory`2")]
public class PagedHistory<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    /// <summary>
    /// Gets or sets the events for this page.
    /// </summary>
    [Id(0)]
    public IReadOnlyList<StoredEvent<TState, TTrigger>> Events { get; set; } = [];

    /// <summary>
    /// Gets or sets the current page number (0-based).
    /// </summary>
    [Id(1)]
    public int Page { get; set; }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    [Id(2)]
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets the total number of events.
    /// </summary>
    [Id(3)]
    public long TotalEvents { get; set; }

    /// <summary>
    /// Gets or sets the total number of pages.
    /// </summary>
    [Id(4)]
    public int TotalPages { get; set; }

    /// <summary>
    /// Gets whether there is a next page.
    /// </summary>
    public bool HasNextPage => Page < TotalPages - 1;

    /// <summary>
    /// Gets whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => Page > 0;
}

/// <summary>
/// Result of a stream compaction operation.
/// </summary>
[GenerateSerializer]
[Alias("Orleans.StateMachineES.Persistence.CompactionResult")]
public class CompactionResult
{
    /// <summary>
    /// Gets or sets whether the compaction was successful.
    /// </summary>
    [Id(0)]
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the snapshot version created.
    /// </summary>
    [Id(1)]
    public long SnapshotVersion { get; set; }

    /// <summary>
    /// Gets or sets the number of events archived.
    /// </summary>
    [Id(2)]
    public int EventsArchived { get; set; }

    /// <summary>
    /// Gets or sets the storage space saved in bytes.
    /// </summary>
    [Id(3)]
    public long SpaceSavedBytes { get; set; }

    /// <summary>
    /// Gets or sets any error message.
    /// </summary>
    [Id(4)]
    public string? ErrorMessage { get; set; }
}
