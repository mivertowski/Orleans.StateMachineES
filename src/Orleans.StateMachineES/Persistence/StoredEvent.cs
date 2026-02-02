namespace Orleans.StateMachineES.Persistence;

/// <summary>
/// Represents an event that has been stored in persistence.
/// Wraps the actual event data with storage metadata.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
[GenerateSerializer]
[Alias("Orleans.StateMachineES.Persistence.StoredEvent`2")]
public class StoredEvent<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    /// <summary>
    /// Gets or sets the unique identifier for this stored event.
    /// </summary>
    [Id(0)]
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stream/aggregate identifier this event belongs to.
    /// </summary>
    [Id(1)]
    public string StreamId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sequence number of this event within the stream.
    /// </summary>
    [Id(2)]
    public long SequenceNumber { get; set; }

    /// <summary>
    /// Gets or sets the type name of the event for deserialization.
    /// </summary>
    [Id(3)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the state before the transition.
    /// </summary>
    [Id(4)]
    public TState FromState { get; set; } = default!;

    /// <summary>
    /// Gets or sets the state after the transition.
    /// </summary>
    [Id(5)]
    public TState ToState { get; set; } = default!;

    /// <summary>
    /// Gets or sets the trigger that caused the transition.
    /// </summary>
    [Id(6)]
    public TTrigger Trigger { get; set; } = default!;

    /// <summary>
    /// Gets or sets the timestamp when the event occurred.
    /// </summary>
    [Id(7)]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the event was stored.
    /// </summary>
    [Id(8)]
    public DateTime StoredAt { get; set; }

    /// <summary>
    /// Gets or sets the correlation ID for tracking related events.
    /// </summary>
    [Id(9)]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the causation ID (the event that caused this event).
    /// </summary>
    [Id(10)]
    public string? CausationId { get; set; }

    /// <summary>
    /// Gets or sets the deduplication key for idempotency.
    /// </summary>
    [Id(11)]
    public string? DedupeKey { get; set; }

    /// <summary>
    /// Gets or sets the version of the state machine that produced this event.
    /// </summary>
    [Id(12)]
    public string? StateMachineVersion { get; set; }

    /// <summary>
    /// Gets or sets additional metadata associated with the event.
    /// </summary>
    [Id(13)]
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Creates an empty stored event.
    /// </summary>
    public StoredEvent()
    {
    }

    /// <summary>
    /// Creates a new stored event from a state transition.
    /// </summary>
    public StoredEvent(
        string streamId,
        long sequenceNumber,
        TState fromState,
        TState toState,
        TTrigger trigger,
        DateTime timestamp,
        string? correlationId = null,
        string? causationId = null,
        string? dedupeKey = null,
        string? stateMachineVersion = null,
        Dictionary<string, object>? metadata = null)
    {
        EventId = Guid.NewGuid().ToString();
        StreamId = streamId;
        SequenceNumber = sequenceNumber;
        EventType = typeof(StoredEvent<TState, TTrigger>).FullName ?? "StoredEvent";
        FromState = fromState;
        ToState = toState;
        Trigger = trigger;
        Timestamp = timestamp;
        StoredAt = DateTime.UtcNow;
        CorrelationId = correlationId;
        CausationId = causationId;
        DedupeKey = dedupeKey;
        StateMachineVersion = stateMachineVersion;
        Metadata = metadata;
    }
}

/// <summary>
/// Represents the result of appending events to the event store.
/// </summary>
[GenerateSerializer]
[Alias("Orleans.StateMachineES.Persistence.AppendResult")]
public class AppendResult
{
    /// <summary>
    /// Gets or sets whether the append operation was successful.
    /// </summary>
    [Id(0)]
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the new stream version after the append.
    /// </summary>
    [Id(1)]
    public long NewVersion { get; set; }

    /// <summary>
    /// Gets or sets any error message if the operation failed.
    /// </summary>
    [Id(2)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the position of the first appended event.
    /// </summary>
    [Id(3)]
    public long FirstEventPosition { get; set; }

    /// <summary>
    /// Gets or sets the number of events appended.
    /// </summary>
    [Id(4)]
    public int EventCount { get; set; }

    /// <summary>
    /// Creates a successful append result.
    /// </summary>
    public static AppendResult Succeeded(long newVersion, long firstPosition, int eventCount)
    {
        return new AppendResult
        {
            Success = true,
            NewVersion = newVersion,
            FirstEventPosition = firstPosition,
            EventCount = eventCount
        };
    }

    /// <summary>
    /// Creates a failed append result.
    /// </summary>
    public static AppendResult Failed(string errorMessage)
    {
        return new AppendResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
