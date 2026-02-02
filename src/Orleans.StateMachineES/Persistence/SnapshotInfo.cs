namespace Orleans.StateMachineES.Persistence;

/// <summary>
/// Represents a snapshot of state machine state for faster recovery.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
[GenerateSerializer]
[Alias("Orleans.StateMachineES.Persistence.SnapshotInfo`1")]
public class SnapshotInfo<TState>
    where TState : notnull
{
    /// <summary>
    /// Gets or sets the unique identifier for this snapshot.
    /// </summary>
    [Id(0)]
    public string SnapshotId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stream/aggregate identifier this snapshot belongs to.
    /// </summary>
    [Id(1)]
    public string StreamId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current state at the time of the snapshot.
    /// </summary>
    [Id(2)]
    public TState State { get; set; } = default!;

    /// <summary>
    /// Gets or sets the version (sequence number) of the last event included in this snapshot.
    /// </summary>
    [Id(3)]
    public long Version { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the snapshot was created.
    /// </summary>
    [Id(4)]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the total number of transitions at snapshot time.
    /// </summary>
    [Id(5)]
    public int TransitionCount { get; set; }

    /// <summary>
    /// Gets or sets the version of the state machine definition.
    /// </summary>
    [Id(6)]
    public string? StateMachineVersion { get; set; }

    /// <summary>
    /// Gets or sets additional metadata associated with the snapshot.
    /// </summary>
    [Id(7)]
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the checksum for data integrity verification.
    /// </summary>
    [Id(8)]
    public string? Checksum { get; set; }

    /// <summary>
    /// Creates an empty snapshot.
    /// </summary>
    public SnapshotInfo()
    {
    }

    /// <summary>
    /// Creates a new snapshot with the specified parameters.
    /// </summary>
    public SnapshotInfo(
        string streamId,
        TState state,
        long version,
        int transitionCount,
        string? stateMachineVersion = null,
        Dictionary<string, object>? metadata = null)
    {
        SnapshotId = Guid.NewGuid().ToString();
        StreamId = streamId;
        State = state;
        Version = version;
        CreatedAt = DateTime.UtcNow;
        TransitionCount = transitionCount;
        StateMachineVersion = stateMachineVersion;
        Metadata = metadata;
    }
}

/// <summary>
/// Options for loading snapshots.
/// </summary>
public class SnapshotLoadOptions
{
    /// <summary>
    /// Gets or sets whether to verify the checksum when loading.
    /// Default: true
    /// </summary>
    public bool VerifyChecksum { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum age of a snapshot to consider valid.
    /// Default: null (no limit)
    /// </summary>
    public TimeSpan? MaxAge { get; set; }

    /// <summary>
    /// Gets or sets whether to prefer the most recent snapshot.
    /// Default: true
    /// </summary>
    public bool PreferMostRecent { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to fail if no snapshot is found.
    /// Default: false
    /// </summary>
    public bool RequireSnapshot { get; set; } = false;
}

/// <summary>
/// Options for saving snapshots.
/// </summary>
public class SnapshotSaveOptions
{
    /// <summary>
    /// Gets or sets whether to calculate and store a checksum.
    /// Default: true
    /// </summary>
    public bool CalculateChecksum { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of snapshots to retain.
    /// Default: 5
    /// </summary>
    public int MaxSnapshotsToRetain { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether to delete old snapshots asynchronously.
    /// Default: true
    /// </summary>
    public bool CleanupAsync { get; set; } = true;

    /// <summary>
    /// Gets or sets additional metadata to include with the snapshot.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}
