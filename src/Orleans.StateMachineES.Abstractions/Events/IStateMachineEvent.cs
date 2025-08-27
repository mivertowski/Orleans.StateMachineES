using System;
using System.Collections.Generic;
using Orleans;

namespace Orleans.StateMachineES.Abstractions.Events;

/// <summary>
/// Base interface for all state machine events.
/// </summary>
public interface IStateMachineEvent
{
    /// <summary>
    /// Gets the timestamp when the event occurred.
    /// </summary>
    DateTime Timestamp { get; }

    /// <summary>
    /// Gets the correlation ID for tracking related events.
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>
    /// Gets the deduplication key for idempotency.
    /// </summary>
    string? DedupeKey { get; }
}

/// <summary>
/// Represents a state transition event in an event-sourced state machine.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
[GenerateSerializer]
public sealed record StateTransitionEvent<TState, TTrigger> : IStateMachineEvent
    where TState : notnull
    where TTrigger : notnull
{
    /// <summary>
    /// Gets the state before the transition.
    /// </summary>
    [Id(0)]
    public TState FromState { get; init; } = default!;

    /// <summary>
    /// Gets the state after the transition.
    /// </summary>
    [Id(1)]
    public TState ToState { get; init; } = default!;

    /// <summary>
    /// Gets the trigger that caused the transition.
    /// </summary>
    [Id(2)]
    public TTrigger Trigger { get; init; } = default!;

    /// <summary>
    /// Gets the timestamp when the transition occurred.
    /// </summary>
    [Id(3)]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

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
}