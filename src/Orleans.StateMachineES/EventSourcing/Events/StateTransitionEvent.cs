namespace Orleans.StateMachineES.EventSourcing.Events;

/// <summary>
/// Represents a state transition event in an event-sourced state machine.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
[GenerateSerializer]
[Alias("Orleans.StateMachineES.EventSourcing.Events.StateTransitionEvent`2")]
public record StateTransitionEvent<TState, TTrigger>
{
    /// <summary>
    /// Gets the state before the transition.
    /// </summary>
    [Id(0)]
    public TState FromState { get; init; }

    /// <summary>
    /// Gets the state after the transition.
    /// </summary>
    [Id(1)]
    public TState ToState { get; init; }

    /// <summary>
    /// Gets the trigger that caused the transition.
    /// </summary>
    [Id(2)]
    public TTrigger Trigger { get; init; }

    /// <summary>
    /// Gets the timestamp when the transition occurred.
    /// </summary>
    [Id(3)]
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the correlation ID for tracking related events.
    /// </summary>
    [Id(4)]
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets the deduplication key for idempotency.
    /// </summary>
    [Id(5)]
    public string? DedupeKey { get; init; }

    /// <summary>
    /// Gets the version of the state machine that produced this event.
    /// </summary>
    [Id(6)]
    public string? StateMachineVersion { get; init; }

    /// <summary>
    /// Gets additional metadata associated with the transition.
    /// </summary>
    [Id(7)]
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Initializes a new instance of the StateTransitionEvent.
    /// </summary>
    public StateTransitionEvent(
        TState fromState,
        TState toState,
        TTrigger trigger,
        DateTime? timestamp = null,
        string? correlationId = null,
        string? dedupeKey = null,
        string? stateMachineVersion = null,
        Dictionary<string, object>? metadata = null)
    {
        FromState = fromState;
        ToState = toState;
        Trigger = trigger;
        Timestamp = timestamp ?? DateTime.UtcNow;
        CorrelationId = correlationId;
        DedupeKey = dedupeKey;
        StateMachineVersion = stateMachineVersion;
        Metadata = metadata;
    }
}

/// <summary>
/// Base interface for all state machine events.
/// </summary>
public interface IStateMachineEvent
{
    DateTime Timestamp { get; }
    string? CorrelationId { get; }
    string? DedupeKey { get; }
}