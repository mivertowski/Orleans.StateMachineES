namespace Orleans.StateMachineES.EventSourcing.Configuration;

/// <summary>
/// Configuration options for event sourcing in state machines.
/// </summary>
public class EventSourcingOptions
{
    /// <summary>
    /// Gets or sets whether to automatically confirm events after raising them.
    /// Default is true.
    /// </summary>
    public bool AutoConfirmEvents { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to publish state transition events to Orleans Streams.
    /// Default is false.
    /// </summary>
    public bool PublishToStream { get; set; } = false;

    /// <summary>
    /// Gets or sets the stream provider name for publishing events.
    /// </summary>
    public string? StreamProvider { get; set; }

    /// <summary>
    /// Gets or sets the stream namespace for publishing events.
    /// Default is "StateMachine".
    /// </summary>
    public string StreamNamespace { get; set; } = "StateMachine";

    /// <summary>
    /// Gets or sets whether to enable idempotency checking using dedupe keys.
    /// Default is true.
    /// </summary>
    public bool EnableIdempotency { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of dedupe keys to keep in memory.
    /// Default is 1000.
    /// </summary>
    public int MaxDedupeKeysInMemory { get; set; } = 1000;

    /// <summary>
    /// Gets or sets whether to enable automatic snapshot creation.
    /// Default is true.
    /// </summary>
    public bool EnableSnapshots { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval (in number of events) between snapshots.
    /// Default is 100.
    /// </summary>
    public int SnapshotInterval { get; set; } = 100;
}