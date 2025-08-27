using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans.StateMachineES.Abstractions.Models;

namespace Orleans.StateMachineES.Abstractions.Interfaces;

/// <summary>
/// Represents a state machine grain that manages states and transitions.
/// This is the core abstraction for all state machine implementations.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers that cause state transitions.</typeparam>
public interface IStateMachineGrain<TState, TTrigger>
    where TState : notnull
    where TTrigger : notnull
{
    /// <summary>
    /// Activates current state in asynchronous fashion. Actions associated with activating the current state
    /// will be invoked. The activation is idempotent and subsequent activation of the same current state
    /// will not lead to re-execution of activation callbacks.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ActivateAsync();

    /// <summary>
    /// Deactivates current state in asynchronous fashion. Actions associated with deactivating the current state
    /// will be invoked. The deactivation is idempotent and subsequent deactivation of the same current state
    /// will not lead to re-execution of deactivation callbacks.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DeactivateAsync();

    /// <summary>
    /// Transition from the current state via the specified trigger in async fashion.
    /// The target state is determined by the configuration of the current state.
    /// Actions associated with leaving the current state and entering the new one will be invoked.
    /// </summary>
    /// <param name="trigger">The trigger to fire.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// The current state does not allow the trigger to be fired.
    /// </exception>
    Task FireAsync(TTrigger trigger);

    /// <summary>
    /// Asynchronously transitions from the current state via the specified trigger with one parameter.
    /// The target state is determined by the configuration of the current state.
    /// Actions associated with leaving the current state and entering the new one will be invoked.
    /// </summary>
    /// <typeparam name="TArg0">The type of the first argument.</typeparam>
    /// <param name="trigger">The trigger to fire.</param>
    /// <param name="arg0">The argument to pass to the trigger.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// The current state does not allow the trigger to be fired.
    /// </exception>
    Task FireAsync<TArg0>(TTrigger trigger, TArg0 arg0);

    /// <summary>
    /// Asynchronously transitions from the current state via the specified trigger with two parameters.
    /// The target state is determined by the configuration of the current state.
    /// Actions associated with leaving the current state and entering the new one will be invoked.
    /// </summary>
    /// <typeparam name="TArg0">The type of the first argument.</typeparam>
    /// <typeparam name="TArg1">The type of the second argument.</typeparam>
    /// <param name="trigger">The trigger to fire.</param>
    /// <param name="arg0">The first argument to pass to the trigger.</param>
    /// <param name="arg1">The second argument to pass to the trigger.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// The current state does not allow the trigger to be fired.
    /// </exception>
    Task FireAsync<TArg0, TArg1>(TTrigger trigger, TArg0 arg0, TArg1 arg1);

    /// <summary>
    /// Asynchronously transitions from the current state via the specified trigger with three parameters.
    /// The target state is determined by the configuration of the current state.
    /// Actions associated with leaving the current state and entering the new one will be invoked.
    /// </summary>
    /// <typeparam name="TArg0">The type of the first argument.</typeparam>
    /// <typeparam name="TArg1">The type of the second argument.</typeparam>
    /// <typeparam name="TArg2">The type of the third argument.</typeparam>
    /// <param name="trigger">The trigger to fire.</param>
    /// <param name="arg0">The first argument to pass to the trigger.</param>
    /// <param name="arg1">The second argument to pass to the trigger.</param>
    /// <param name="arg2">The third argument to pass to the trigger.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// The current state does not allow the trigger to be fired.
    /// </exception>
    Task FireAsync<TArg0, TArg1, TArg2>(TTrigger trigger, TArg0 arg0, TArg1 arg1, TArg2 arg2);

    /// <summary>
    /// Gets the current state of the state machine asynchronously.
    /// </summary>
    /// <returns>A ValueTask that represents the asynchronous operation. The task result contains the current state.</returns>
    ValueTask<TState> GetStateAsync();

    /// <summary>
    /// Determines whether the state machine is in the specified state asynchronously.
    /// </summary>
    /// <param name="state">The state to check.</param>
    /// <returns>
    /// A ValueTask that represents the asynchronous operation. The task result contains true if the current state is equal to, or a substate of,
    /// the specified state; otherwise, false.
    /// </returns>
    ValueTask<bool> IsInStateAsync(TState state);

    /// <summary>
    /// Determines whether the specified trigger can be fired in the current state asynchronously.
    /// </summary>
    /// <param name="trigger">The trigger to check.</param>
    /// <returns>
    /// A ValueTask that represents the asynchronous operation. The task result contains true if the trigger can be fired in the current state; otherwise, false.
    /// </returns>
    ValueTask<bool> CanFireAsync(TTrigger trigger);

    /// <summary>
    /// Gets information about the state machine configuration and current state.
    /// </summary>
    /// <returns>A ValueTask that represents the asynchronous operation. The task result contains a <see cref="StateMachineInfo"/> object.</returns>
    ValueTask<StateMachineInfo> GetInfoAsync();

    /// <summary>
    /// Gets the permitted triggers for the current state.
    /// </summary>
    /// <param name="args">Optional arguments for conditional triggers.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The task result contains an enumerable collection of permitted triggers.</returns>
    ValueTask<IEnumerable<TTrigger>> GetPermittedTriggersAsync(params object[] args);

    /// <summary>
    /// Gets detailed information about permitted triggers for the current state.
    /// </summary>
    /// <param name="args">Optional arguments for conditional triggers.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The task result contains an enumerable collection of <see cref="TriggerDetails{TState, TTrigger}"/>.</returns>
    ValueTask<IEnumerable<TriggerDetails<TState, TTrigger>>> GetDetailedPermittedTriggersAsync(params object[] args);

    /// <summary>
    /// Returns a string representation of the state machine asynchronously.
    /// </summary>
    /// <returns>A ValueTask that represents the asynchronous operation. The task result contains a string describing the state machine.</returns>
    ValueTask<string> ToStringAsync();
}