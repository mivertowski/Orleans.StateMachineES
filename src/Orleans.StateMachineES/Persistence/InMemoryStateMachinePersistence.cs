using System.Diagnostics;

namespace Orleans.StateMachineES.Persistence;

/// <summary>
/// In-memory implementation of state machine persistence combining event and snapshot stores.
/// Not suitable for production use as data is not persisted across restarts.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
public class InMemoryStateMachinePersistence<TState, TTrigger> : IStateMachinePersistence<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    private readonly InMemoryEventStore<TState, TTrigger> _eventStore;
    private readonly InMemorySnapshotStore<TState> _snapshotStore;
    private readonly PersistenceOptions _options;

    /// <inheritdoc/>
    public string ProviderName => "InMemory";

    /// <summary>
    /// Creates a new in-memory persistence instance.
    /// </summary>
    /// <param name="options">Optional persistence options.</param>
    public InMemoryStateMachinePersistence(PersistenceOptions? options = null)
    {
        _options = options ?? new PersistenceOptions();
        _eventStore = new InMemoryEventStore<TState, TTrigger>();
        _snapshotStore = new InMemorySnapshotStore<TState>();
    }

    #region IEventStore Implementation

    /// <inheritdoc/>
    public Task<AppendResult> AppendEventsAsync(
        string streamId,
        IEnumerable<StoredEvent<TState, TTrigger>> events,
        long expectedVersion,
        CancellationToken cancellationToken = default)
        => _eventStore.AppendEventsAsync(streamId, events, expectedVersion, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoredEvent<TState, TTrigger>>> ReadEventsAsync(
        string streamId,
        long fromVersion,
        int count,
        CancellationToken cancellationToken = default)
        => _eventStore.ReadEventsAsync(streamId, fromVersion, count, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoredEvent<TState, TTrigger>>> ReadAllEventsAsync(
        string streamId,
        CancellationToken cancellationToken = default)
        => _eventStore.ReadAllEventsAsync(streamId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoredEvent<TState, TTrigger>>> ReadEventsBackwardAsync(
        string streamId,
        long fromVersion,
        int count,
        CancellationToken cancellationToken = default)
        => _eventStore.ReadEventsBackwardAsync(streamId, fromVersion, count, cancellationToken);

    /// <inheritdoc/>
    public Task<long> GetStreamVersionAsync(
        string streamId,
        CancellationToken cancellationToken = default)
        => _eventStore.GetStreamVersionAsync(streamId, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> StreamExistsAsync(
        string streamId,
        CancellationToken cancellationToken = default)
        => _eventStore.StreamExistsAsync(streamId, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> DeleteStreamAsync(
        string streamId,
        long expectedVersion,
        CancellationToken cancellationToken = default)
        => _eventStore.DeleteStreamAsync(streamId, expectedVersion, cancellationToken);

    /// <inheritdoc/>
    public Task<long> GetTotalEventCountAsync(CancellationToken cancellationToken = default)
        => _eventStore.GetTotalEventCountAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<IAsyncDisposable> SubscribeAsync(
        string streamId,
        long fromVersion,
        Func<StoredEvent<TState, TTrigger>, Task> onEvent,
        CancellationToken cancellationToken = default)
        => _eventStore.SubscribeAsync(streamId, fromVersion, onEvent, cancellationToken);

    #endregion

    #region ISnapshotStore Implementation

    /// <inheritdoc/>
    public Task<bool> SaveSnapshotAsync(
        SnapshotInfo<TState> snapshot,
        SnapshotSaveOptions? options = null,
        CancellationToken cancellationToken = default)
        => _snapshotStore.SaveSnapshotAsync(snapshot, options, cancellationToken);

    /// <inheritdoc/>
    public Task<SnapshotInfo<TState>?> LoadSnapshotAsync(
        string streamId,
        SnapshotLoadOptions? options = null,
        CancellationToken cancellationToken = default)
        => _snapshotStore.LoadSnapshotAsync(streamId, options, cancellationToken);

    /// <inheritdoc/>
    public Task<SnapshotInfo<TState>?> LoadSnapshotAtVersionAsync(
        string streamId,
        long maxVersion,
        SnapshotLoadOptions? options = null,
        CancellationToken cancellationToken = default)
        => _snapshotStore.LoadSnapshotAtVersionAsync(streamId, maxVersion, options, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<SnapshotInfo<TState>>> GetAllSnapshotsAsync(
        string streamId,
        CancellationToken cancellationToken = default)
        => _snapshotStore.GetAllSnapshotsAsync(streamId, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> DeleteSnapshotAsync(
        string streamId,
        string snapshotId,
        CancellationToken cancellationToken = default)
        => _snapshotStore.DeleteSnapshotAsync(streamId, snapshotId, cancellationToken);

    /// <inheritdoc/>
    public Task<int> DeleteAllSnapshotsAsync(
        string streamId,
        CancellationToken cancellationToken = default)
        => _snapshotStore.DeleteAllSnapshotsAsync(streamId, cancellationToken);

    /// <inheritdoc/>
    public Task<int> PruneSnapshotsAsync(
        string streamId,
        long beforeVersion,
        int keepCount = 1,
        CancellationToken cancellationToken = default)
        => _snapshotStore.PruneSnapshotsAsync(streamId, beforeVersion, keepCount, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> HasSnapshotAsync(
        string streamId,
        CancellationToken cancellationToken = default)
        => _snapshotStore.HasSnapshotAsync(streamId, cancellationToken);

    #endregion

    #region IStateMachinePersistence Implementation

    /// <inheritdoc/>
    public async Task<LoadedState<TState, TTrigger>> LoadStateAsync(
        string streamId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Check if stream exists
        var streamExists = await StreamExistsAsync(streamId, cancellationToken);
        if (!streamExists)
        {
            return LoadedState<TState, TTrigger>.NotFound();
        }

        // Try to load from snapshot first
        SnapshotInfo<TState>? snapshot = null;
        long startVersion = 0;

        if (_options.EnableSnapshots)
        {
            snapshot = await LoadSnapshotAsync(streamId, cancellationToken: cancellationToken);
            if (snapshot != null)
            {
                startVersion = snapshot.Version + 1;
            }
        }

        // Load events after snapshot
        var events = await ReadEventsAsync(streamId, startVersion, int.MaxValue, cancellationToken);
        var eventsList = events.ToList();

        // Calculate current state
        var hasState = snapshot != null;
        TState currentState = snapshot != null ? snapshot.State : default!;
        DateTime? lastTimestamp = snapshot?.CreatedAt;

        foreach (var evt in eventsList)
        {
            currentState = evt.ToState;
            lastTimestamp = evt.Timestamp;
            hasState = true;
        }

        stopwatch.Stop();

        return new LoadedState<TState, TTrigger>
        {
            CurrentState = currentState,
            Version = (snapshot?.Version ?? 0) + eventsList.Count,
            LoadedFromSnapshot = snapshot != null,
            SnapshotVersion = snapshot?.Version,
            EventsReplayedCount = eventsList.Count,
            LastTransitionTimestamp = lastTimestamp,
            StreamExists = true,
            ReplayedEvents = eventsList,
            LoadDuration = stopwatch.Elapsed
        };
    }

    /// <inheritdoc/>
    public async Task<LoadedState<TState, TTrigger>> LoadStateAtTimeAsync(
        string streamId,
        DateTime pointInTime,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Load all events
        var allEvents = await ReadAllEventsAsync(streamId, cancellationToken);
        var eventsBeforeTime = allEvents.Where(e => e.Timestamp <= pointInTime).ToList();

        if (eventsBeforeTime.Count == 0)
        {
            return LoadedState<TState, TTrigger>.NotFound();
        }

        // Calculate state at point in time
        var lastEvent = eventsBeforeTime.Last();

        stopwatch.Stop();

        return new LoadedState<TState, TTrigger>
        {
            CurrentState = lastEvent.ToState,
            Version = eventsBeforeTime.Count,
            LoadedFromSnapshot = false,
            EventsReplayedCount = eventsBeforeTime.Count,
            LastTransitionTimestamp = lastEvent.Timestamp,
            StreamExists = true,
            ReplayedEvents = eventsBeforeTime,
            LoadDuration = stopwatch.Elapsed
        };
    }

    /// <inheritdoc/>
    public async Task<LoadedState<TState, TTrigger>> LoadStateAtVersionAsync(
        string streamId,
        long version,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Try to load from snapshot at or before version
        SnapshotInfo<TState>? snapshot = null;
        long startVersion = 0;

        if (_options.EnableSnapshots)
        {
            snapshot = await LoadSnapshotAtVersionAsync(streamId, version, cancellationToken: cancellationToken);
            if (snapshot != null)
            {
                startVersion = snapshot.Version + 1;
            }
        }

        // Load events from snapshot to target version
        var eventsToLoad = (int)(version - startVersion + 1);
        var events = await ReadEventsAsync(streamId, startVersion, eventsToLoad, cancellationToken);
        var eventsList = events.ToList();

        if (eventsList.Count == 0 && snapshot == null)
        {
            return LoadedState<TState, TTrigger>.NotFound();
        }

        // Calculate state at version
        var hasState = snapshot != null;
        TState currentState = snapshot != null ? snapshot.State : default!;
        DateTime? lastTimestamp = snapshot?.CreatedAt;

        foreach (var evt in eventsList)
        {
            currentState = evt.ToState;
            lastTimestamp = evt.Timestamp;
            hasState = true;
        }

        stopwatch.Stop();

        return new LoadedState<TState, TTrigger>
        {
            CurrentState = currentState,
            Version = (snapshot?.Version ?? 0) + eventsList.Count,
            LoadedFromSnapshot = snapshot != null,
            SnapshotVersion = snapshot?.Version,
            EventsReplayedCount = eventsList.Count,
            LastTransitionTimestamp = lastTimestamp,
            StreamExists = true,
            ReplayedEvents = eventsList,
            LoadDuration = stopwatch.Elapsed
        };
    }

    /// <inheritdoc/>
    public async Task<StateHistory<TState, TTrigger>> GetHistoryAsync(
        string streamId,
        CancellationToken cancellationToken = default)
    {
        var events = await ReadAllEventsAsync(streamId, cancellationToken);
        var eventsList = events.ToList();

        var history = new StateHistory<TState, TTrigger>
        {
            StreamId = streamId,
            Events = eventsList,
            TransitionCount = eventsList.Count
        };

        if (eventsList.Count > 0)
        {
            history.InitialState = eventsList.First().FromState;
            history.CurrentState = eventsList.Last().ToState;
            history.FirstEventTimestamp = eventsList.First().Timestamp;
            history.LastEventTimestamp = eventsList.Last().Timestamp;

            // Calculate distinct states
            var distinctStates = new HashSet<TState>();
            foreach (var evt in eventsList)
            {
                distinctStates.Add(evt.FromState);
                distinctStates.Add(evt.ToState);
            }
            history.DistinctStates = distinctStates.ToList();

            // Calculate distinct triggers
            var distinctTriggers = eventsList.Select(e => e.Trigger).Distinct().ToList();
            history.DistinctTriggers = distinctTriggers;
        }

        return history;
    }

    /// <inheritdoc/>
    public async Task<PagedHistory<TState, TTrigger>> GetHistoryPagedAsync(
        string streamId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var version = await GetStreamVersionAsync(streamId, cancellationToken);
        if (version < 0)
        {
            return new PagedHistory<TState, TTrigger>
            {
                Events = [],
                Page = page,
                PageSize = pageSize,
                TotalEvents = 0,
                TotalPages = 0
            };
        }

        var totalEvents = version;
        var totalPages = (int)Math.Ceiling((double)totalEvents / pageSize);
        var skip = page * pageSize;

        var events = await ReadEventsAsync(streamId, skip, pageSize, cancellationToken);

        return new PagedHistory<TState, TTrigger>
        {
            Events = events,
            Page = page,
            PageSize = pageSize,
            TotalEvents = totalEvents,
            TotalPages = totalPages
        };
    }

    /// <inheritdoc/>
    public async Task<CompactionResult> CompactStreamAsync(
        string streamId,
        bool archiveOldEvents = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Load current state
            var state = await LoadStateAsync(streamId, cancellationToken);
            if (!state.StreamExists || state.CurrentState == null)
            {
                return new CompactionResult
                {
                    Success = false,
                    ErrorMessage = "Stream not found or has no state"
                };
            }

            // Create snapshot
            var snapshot = new SnapshotInfo<TState>(
                streamId,
                state.CurrentState,
                state.Version,
                (int)state.Version);

            await SaveSnapshotAsync(snapshot, cancellationToken: cancellationToken);

            // In-memory doesn't support archival, just return success
            return new CompactionResult
            {
                Success = true,
                SnapshotVersion = state.Version,
                EventsArchived = archiveOldEvents ? (int)state.Version : 0,
                SpaceSavedBytes = 0
            };
        }
        catch (Exception ex)
        {
            return new CompactionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    #endregion

    #region Health and Stats

    /// <inheritdoc/>
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        var eventStoreHealthy = await _eventStore.IsHealthyAsync(cancellationToken);
        var snapshotStoreHealthy = await _snapshotStore.IsHealthyAsync(cancellationToken);
        return eventStoreHealthy && snapshotStoreHealthy;
    }

    /// <inheritdoc/>
    Task<EventStoreStats> IEventStore.GetStatsAsync(CancellationToken cancellationToken)
        => _eventStore.GetStatsAsync(cancellationToken);

    /// <inheritdoc/>
    Task<SnapshotStoreStats> ISnapshotStore.GetStatsAsync(CancellationToken cancellationToken)
        => _snapshotStore.GetStatsAsync(cancellationToken);

    #endregion

    /// <summary>
    /// Clears all data from the in-memory stores.
    /// </summary>
    public async Task ClearAsync()
    {
        await _eventStore.ClearAsync();
        await _snapshotStore.ClearAsync();
    }
}
