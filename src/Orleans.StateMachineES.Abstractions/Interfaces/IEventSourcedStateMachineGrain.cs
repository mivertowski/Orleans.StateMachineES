using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;
using Orleans.StateMachineES.Abstractions.Events;

namespace Orleans.StateMachineES.Abstractions.Interfaces;

/// <summary>
/// Represents an event-sourced state machine grain that persists state transitions as events.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
public interface IEventSourcedStateMachineGrain<TState, TTrigger> : 
    IStateMachineGrain<TState, TTrigger>,
    IGrainWithGuidKey
    where TState : notnull
    where TTrigger : notnull
{
    /// <summary>
    /// Gets the count of transitions that have occurred.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the transition count.</returns>
    Task<int> GetTransitionCountAsync();

    /// <summary>
    /// Gets the correlation ID of the last transition.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the correlation ID.</returns>
    Task<string?> GetLastCorrelationIdAsync();

    /// <summary>
    /// Sets the correlation ID for the next transition.
    /// </summary>
    /// <param name="correlationId">The correlation ID to set.</param>
    void SetCorrelationId(string correlationId);

    /// <summary>
    /// Gets the event history for this state machine.
    /// </summary>
    /// <param name="fromVersion">The starting version to retrieve events from.</param>
    /// <param name="toVersion">The ending version to retrieve events to.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the event history.</returns>
    Task<IReadOnlyList<StateTransitionEvent<TState, TTrigger>>> GetEventHistoryAsync(
        int fromVersion = 0, 
        int? toVersion = null);

    /// <summary>
    /// Creates a snapshot of the current state.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task CreateSnapshotAsync();

    /// <summary>
    /// Gets the version of the current snapshot.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the snapshot version.</returns>
    Task<int> GetSnapshotVersionAsync();
}