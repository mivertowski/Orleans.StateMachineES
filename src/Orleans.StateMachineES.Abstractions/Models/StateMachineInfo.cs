using System;
using System.Collections.Generic;
using Orleans;

namespace Orleans.StateMachineES.Abstractions.Models;

/// <summary>
/// Represents information about a state machine configuration.
/// </summary>
[GenerateSerializer]
public sealed class StateMachineInfo
{
    /// <summary>
    /// Gets the current state of the state machine.
    /// </summary>
    [Id(0)]
    public string CurrentState { get; init; } = string.Empty;

    /// <summary>
    /// Gets the list of all configured states.
    /// </summary>
    [Id(1)]
    public IReadOnlyList<string> States { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the list of triggers permitted in the current state.
    /// </summary>
    [Id(2)]
    public IReadOnlyList<string> PermittedTriggers { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets detailed information about state transitions.
    /// </summary>
    [Id(3)]
    public IReadOnlyList<StateTransition> Transitions { get; init; } = Array.Empty<StateTransition>();

    /// <summary>
    /// Gets the hierarchical relationships between states.
    /// </summary>
    [Id(4)]
    public IReadOnlyDictionary<string, IReadOnlyList<string>> StateHierarchy { get; init; } = 
        new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>
    /// Gets the timestamp when this information was captured.
    /// </summary>
    [Id(5)]
    public DateTime CapturedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a state transition in a state machine.
/// </summary>
[GenerateSerializer]
public sealed class StateTransition
{
    /// <summary>
    /// Gets the source state of the transition.
    /// </summary>
    [Id(0)]
    public string FromState { get; init; } = string.Empty;

    /// <summary>
    /// Gets the target state of the transition.
    /// </summary>
    [Id(1)]
    public string ToState { get; init; } = string.Empty;

    /// <summary>
    /// Gets the trigger that causes this transition.
    /// </summary>
    [Id(2)]
    public string Trigger { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether this transition has guard conditions.
    /// </summary>
    [Id(3)]
    public bool HasGuards { get; init; }

    /// <summary>
    /// Gets whether this transition has actions.
    /// </summary>
    [Id(4)]
    public bool HasActions { get; init; }

    /// <summary>
    /// Gets the description of guard conditions, if any.
    /// </summary>
    [Id(5)]
    public string? GuardDescription { get; init; }
}

/// <summary>
/// Represents detailed information about a trigger and its possible destinations.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
[GenerateSerializer]
public sealed class TriggerDetails<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    /// <summary>
    /// Gets the trigger.
    /// </summary>
    [Id(0)]
    public TTrigger Trigger { get; init; } = default!;

    /// <summary>
    /// Gets the possible destination states for this trigger.
    /// </summary>
    [Id(1)]
    public IReadOnlyList<TState> PossibleDestinations { get; init; } = Array.Empty<TState>();

    /// <summary>
    /// Gets whether this trigger has guard conditions.
    /// </summary>
    [Id(2)]
    public bool HasGuards { get; init; }

    /// <summary>
    /// Gets whether this trigger has actions.
    /// </summary>
    [Id(3)]
    public bool HasActions { get; init; }

    /// <summary>
    /// Gets the description of this trigger.
    /// </summary>
    [Id(4)]
    public string Description { get; init; } = string.Empty;
}