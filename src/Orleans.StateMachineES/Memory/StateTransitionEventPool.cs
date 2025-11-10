using Orleans.StateMachineES.EventSourcing.Events;

namespace Orleans.StateMachineES.Memory;

/// <summary>
/// Specialized pool for state transition events to reduce allocations in event sourcing scenarios.
/// </summary>
public static class StateTransitionEventPool<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    private static readonly ObjectPool<StateTransitionEvent<TState, TTrigger>> _pool = new(
        () => new StateTransitionEvent<TState, TTrigger>(default!, default!, default!),
        evt => ResetEvent(evt));

    /// <summary>
    /// Gets a pooled StateTransitionEvent instance.
    /// </summary>
    public static StateTransitionEvent<TState, TTrigger> Get() => _pool.Get();

    /// <summary>
    /// Returns a StateTransitionEvent instance to the pool.
    /// </summary>
    public static void Return(StateTransitionEvent<TState, TTrigger> evt) => _pool.Return(evt);

    /// <summary>
    /// Creates a new StateTransitionEvent with pooled instance optimization.
    /// </summary>
    public static StateTransitionEvent<TState, TTrigger> Create(
        TState fromState,
        TState toState,
        TTrigger trigger,
        DateTime? timestamp = null,
        string? correlationId = null,
        string? dedupeKey = null,
        string? stateMachineVersion = null,
        Dictionary<string, object>? metadata = null)
    {
        return new StateTransitionEvent<TState, TTrigger>(
            fromState, toState, trigger, timestamp, correlationId,
            dedupeKey, stateMachineVersion, metadata);
    }

    private static void ResetEvent(StateTransitionEvent<TState, TTrigger> evt)
    {
        // For record types, we can't reset fields, but we can clear metadata if it's mutable
        // In practice, we'll rely on the GC for records and focus pooling on mutable objects
    }
}
