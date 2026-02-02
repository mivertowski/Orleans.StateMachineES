namespace Orleans.StateMachineES.Persistence;

/// <summary>
/// Interface for snapshot store implementations that persist state machine snapshots.
/// Snapshots enable faster recovery by avoiding full event replay.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
public interface ISnapshotStore<TState>
    where TState : notnull
{
    /// <summary>
    /// Saves a snapshot of the current state.
    /// </summary>
    /// <param name="snapshot">The snapshot to save.</param>
    /// <param name="options">Options for saving the snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the snapshot was saved successfully.</returns>
    Task<bool> SaveSnapshotAsync(
        SnapshotInfo<TState> snapshot,
        SnapshotSaveOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the most recent snapshot for a stream.
    /// </summary>
    /// <param name="streamId">The unique identifier for the stream.</param>
    /// <param name="options">Options for loading the snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The snapshot, or null if none exists.</returns>
    Task<SnapshotInfo<TState>?> LoadSnapshotAsync(
        string streamId,
        SnapshotLoadOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a snapshot at or before a specific version.
    /// </summary>
    /// <param name="streamId">The unique identifier for the stream.</param>
    /// <param name="maxVersion">The maximum version to consider.</param>
    /// <param name="options">Options for loading the snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The snapshot, or null if none exists.</returns>
    Task<SnapshotInfo<TState>?> LoadSnapshotAtVersionAsync(
        string streamId,
        long maxVersion,
        SnapshotLoadOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all snapshots for a stream.
    /// </summary>
    /// <param name="streamId">The unique identifier for the stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All snapshots for the stream, ordered by version descending.</returns>
    Task<IReadOnlyList<SnapshotInfo<TState>>> GetAllSnapshotsAsync(
        string streamId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a specific snapshot.
    /// </summary>
    /// <param name="streamId">The unique identifier for the stream.</param>
    /// <param name="snapshotId">The unique identifier for the snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the snapshot was deleted.</returns>
    Task<bool> DeleteSnapshotAsync(
        string streamId,
        string snapshotId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all snapshots for a stream.
    /// </summary>
    /// <param name="streamId">The unique identifier for the stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of snapshots deleted.</returns>
    Task<int> DeleteAllSnapshotsAsync(
        string streamId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes snapshots older than a specific version.
    /// </summary>
    /// <param name="streamId">The unique identifier for the stream.</param>
    /// <param name="beforeVersion">Delete snapshots before this version.</param>
    /// <param name="keepCount">Number of recent snapshots to keep regardless of version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of snapshots deleted.</returns>
    Task<int> PruneSnapshotsAsync(
        string streamId,
        long beforeVersion,
        int keepCount = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a snapshot exists for a stream.
    /// </summary>
    /// <param name="streamId">The unique identifier for the stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if at least one snapshot exists.</returns>
    Task<bool> HasSnapshotAsync(
        string streamId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Non-generic interface for snapshot store operations.
/// </summary>
public interface ISnapshotStore
{
    /// <summary>
    /// Gets the name of this snapshot store provider.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Checks if the snapshot store is available and healthy.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if healthy, false otherwise.</returns>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about the snapshot store.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Snapshot store statistics.</returns>
    Task<SnapshotStoreStats> GetStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about a snapshot store.
/// </summary>
public class SnapshotStoreStats
{
    /// <summary>
    /// Gets or sets the total number of streams with snapshots.
    /// </summary>
    public long StreamCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of snapshots.
    /// </summary>
    public long SnapshotCount { get; set; }

    /// <summary>
    /// Gets or sets the total storage size in bytes.
    /// </summary>
    public long StorageSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the oldest snapshot timestamp.
    /// </summary>
    public DateTime? OldestSnapshotTimestamp { get; set; }

    /// <summary>
    /// Gets or sets the newest snapshot timestamp.
    /// </summary>
    public DateTime? NewestSnapshotTimestamp { get; set; }

    /// <summary>
    /// Gets or sets additional provider-specific statistics.
    /// </summary>
    public Dictionary<string, object>? ExtendedStats { get; set; }
}
