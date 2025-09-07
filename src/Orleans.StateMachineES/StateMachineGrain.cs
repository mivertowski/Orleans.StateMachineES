using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Orleans.StateMachineES.Extensions;
using Orleans.StateMachineES.Interfaces;
using Orleans.StateMachineES.Models;
using Orleans;
using Stateless;
using Stateless.Graph;

namespace Orleans.StateMachineES;

/// <summary>
/// Base grain class for integrating a Stateless state machine with Orleans.
/// Provides methods for firing triggers, querying state, and retrieving state machine metadata.
/// </summary>
/// <typeparam name="TState">The type representing the states.</typeparam>
/// <typeparam name="TTrigger">The type representing the triggers.</typeparam>
public abstract class StateMachineGrain<TState, TTrigger> : Grain, IStateMachineGrain<TState, TTrigger>
{
    [NotNull]
    protected StateMachine<TState, TTrigger>? StateMachine { get; private set; }

    /// <summary>
    /// Thread-local flag to detect if we're currently executing within a state callback.
    /// </summary>
    [ThreadStatic]
    private static bool _isInStateCallback;

    /// <summary>
    /// Gets whether we're currently executing within a state callback (OnEntry, OnExit, etc.).
    /// </summary>
    protected static bool IsInStateCallback => _isInStateCallback;

    /// <summary>
    /// Sets the callback execution context flag.
    /// </summary>
    protected internal static void SetCallbackContext(bool isInCallback) => _isInStateCallback = isInCallback;

    /// <summary>
    /// Activates the state machine.
    /// </summary>
    public async Task ActivateAsync()
    {
        await StateMachine.ActivateAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Deactivates the state machine.
    /// </summary>
    public async Task DeactivateAsync()
    {
        await StateMachine.DeactivateAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Fires the specified trigger asynchronously.
    /// </summary>
    /// <param name="trigger">The trigger to fire.</param>
    public virtual async Task FireAsync(TTrigger trigger)
    {
        // Check if we're being called from within a state callback
        if (IsInStateCallback)
        {
            throw new InvalidOperationException(
                $"FireAsync cannot be called from within state callbacks (OnEntry, OnExit, etc.). " +
                $"Trigger: {trigger}. Move state transitions to grain methods that execute after callbacks complete.");
        }

        await StateMachine.FireAsync(trigger).ConfigureAwait(false);
    }

    /// <summary>
    /// Fires the specified trigger with one argument asynchronously.
    /// </summary>
    public async Task FireAsync<TArg0>(TTrigger trigger, TArg0 arg0)
    {
        // Check if we're being called from within a state callback
        if (IsInStateCallback)
        {
            throw new InvalidOperationException(
                $"FireAsync cannot be called from within state callbacks (OnEntry, OnExit, etc.). " +
                $"Trigger: {trigger}. Move state transitions to grain methods that execute after callbacks complete.");
        }

        var tp = StateMachine.SetTriggerParameters<TArg0>(trigger);
        await StateMachine.FireAsync(tp, arg0).ConfigureAwait(false);
    }

    /// <summary>
    /// Fires the specified trigger with two arguments asynchronously.
    /// </summary>
    public async Task FireAsync<TArg0, TArg1>(TTrigger trigger, TArg0 arg0, TArg1 arg1)
    {
        // Check if we're being called from within a state callback
        if (IsInStateCallback)
        {
            throw new InvalidOperationException(
                $"FireAsync cannot be called from within state callbacks (OnEntry, OnExit, etc.). " +
                $"Trigger: {trigger}. Move state transitions to grain methods that execute after callbacks complete.");
        }

        var tp = StateMachine.SetTriggerParameters<TArg0, TArg1>(trigger);
        await StateMachine.FireAsync(tp, arg0, arg1).ConfigureAwait(false);
    }

    /// <summary>
    /// Fires the specified trigger with three arguments asynchronously.
    /// </summary>
    public async Task FireAsync<TArg0, TArg1, TArg2>(TTrigger trigger, TArg0 arg0, TArg1 arg1, TArg2 arg2)
    {
        // Check if we're being called from within a state callback
        if (IsInStateCallback)
        {
            throw new InvalidOperationException(
                $"FireAsync cannot be called from within state callbacks (OnEntry, OnExit, etc.). " +
                $"Trigger: {trigger}. Move state transitions to grain methods that execute after callbacks complete.");
        }

        var tp = StateMachine.SetTriggerParameters<TArg0, TArg1, TArg2>(trigger);
        await StateMachine.FireAsync(tp, arg0, arg1, arg2).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the current state asynchronously.
    /// </summary>
    public ValueTask<TState> GetStateAsync()
    {
        return ValueTaskExtensions.FromResult(StateMachine.State);
    }

    /// <summary>
    /// Determines whether the state machine is in the specified state.
    /// </summary>
    public ValueTask<bool> IsInStateAsync(TState state)
    {
        return ValueTaskExtensions.FromResult(StateMachine.IsInState(state));
    }

    /// <summary>
    /// Determines whether the specified trigger can be fired.
    /// </summary>
    public ValueTask<bool> CanFireAsync(TTrigger trigger)
    {
        return ValueTaskExtensions.FromResult(StateMachine.CanFire(trigger));
    }

    /// <summary>
    /// Gets information about the state machine.
    /// </summary>
    public ValueTask<OrleansStateMachineInfo> GetInfoAsync()
    {
        return ValueTaskExtensions.FromResult(new OrleansStateMachineInfo(StateMachine.GetInfo()));
    }

    /// <summary>
    /// Gets the permitted triggers for the current state.
    /// </summary>
    public ValueTask<IEnumerable<TTrigger>> GetPermittedTriggersAsync(params object[] args)
    {
        return ValueTaskExtensions.FromResult(StateMachine.GetPermittedTriggers(args));
    }

    /// <summary>
    /// Gets detailed information about permitted triggers for the current state.
    /// </summary>
    public ValueTask<IEnumerable<TriggerDetails<TState, TTrigger>>> GetDetailedPermittedTriggersAsync(params object[] args)
    {
        return ValueTaskExtensions.FromResult(StateMachine.GetDetailedPermittedTriggers(args));
    }

    /// <summary>
    /// Gets the permitted triggers property for the current state.
    /// </summary>
    public ValueTask<IEnumerable<TTrigger>> GetPermittedTriggersPropertyAsync()
    {
        return ValueTaskExtensions.FromResult(StateMachine.PermittedTriggers);
    }

    /// <summary>
    /// Determines whether the specified trigger can be fired and returns unmet guard conditions.
    /// </summary>
    public ValueTask<(bool, ICollection<string>)> CanFireWithUnmetGuardsAsync(TTrigger trigger)
    {
        var result = StateMachine.CanFire(trigger, out var unmetGuards);
        return ValueTaskExtensions.FromResult((result, unmetGuards));
    }

    /// <summary>
    /// Determines whether the specified trigger with one argument can be fired.
    /// </summary>
    public ValueTask<bool> CanFireAsync<TArg0>(TTrigger trigger, TArg0 arg0)
    {
        var tp = StateMachine.SetTriggerParameters<TArg0>(trigger);
        return ValueTaskExtensions.FromResult(StateMachine.CanFire(tp, arg0));
    }

    /// <summary>
    /// Determines whether the specified trigger with one argument can be fired and returns unmet guard conditions.
    /// </summary>
    public ValueTask<(bool, ICollection<string>)> CanFireWithUnmetGuardsAsync<TArg0>(TTrigger trigger, TArg0 arg0)
    {
        var tp = StateMachine.SetTriggerParameters<TArg0>(trigger);
        var result = StateMachine.CanFire(tp, arg0, out var unmet);
        return ValueTaskExtensions.FromResult((result, unmet));
    }

    /// <summary>
    /// Determines whether the specified trigger with two arguments can be fired.
    /// </summary>
    public ValueTask<bool> CanFireAsync<TArg0, TArg1>(TTrigger trigger, TArg0 arg0, TArg1 arg1)
    {
        var tp = StateMachine.SetTriggerParameters<TArg0, TArg1>(trigger);
        return ValueTaskExtensions.FromResult(StateMachine.CanFire(tp, arg0, arg1));
    }

    /// <summary>
    /// Determines whether the specified trigger with two arguments can be fired and returns unmet guard conditions.
    /// </summary>
    public ValueTask<(bool, ICollection<string>)> CanFireWithUnmetGuardsAsync<TArg0, TArg1>(TTrigger trigger, TArg0 arg0, TArg1 arg1)
    {
        var tp = StateMachine.SetTriggerParameters<TArg0, TArg1>(trigger);
        var result = StateMachine.CanFire(tp, arg0, arg1, out var unmet);
        return ValueTaskExtensions.FromResult((result, unmet));
    }

    /// <summary>
    /// Determines whether the specified trigger with three arguments can be fired.
    /// </summary>
    public ValueTask<bool> CanFireAsync<TArg0, TArg1, TArg2>(TTrigger trigger, TArg0 arg0, TArg1 arg1, TArg2 arg2)
    {
        var tp = StateMachine.SetTriggerParameters<TArg0, TArg1, TArg2>(trigger);
        return ValueTaskExtensions.FromResult(StateMachine.CanFire(tp, arg0, arg1, arg2));
    }

    /// <summary>
    /// Determines whether the specified trigger with three arguments can be fired and returns unmet guard conditions.
    /// </summary>
    public ValueTask<(bool, ICollection<string>)> CanFireWithUnmetGuardsAsync<TArg0, TArg1, TArg2>(TTrigger trigger, TArg0 arg0, TArg1 arg1, TArg2 arg2)
    {
        var tp = StateMachine.SetTriggerParameters<TArg0, TArg1, TArg2>(trigger);
        var result = StateMachine.CanFire(tp, arg0, arg1, arg2, out var unmet);
        return ValueTaskExtensions.FromResult((result, unmet));
    }
    
    /// <summary>
    /// Returns a string representation of the state machine.
    /// </summary>
    public ValueTask<string> ToStringAsync()
    {
        return ValueTaskExtensions.FromResult(StateMachine.ToString());
    }

    /// <summary>
    /// Builds the state machine instance.
    /// </summary>
    protected abstract StateMachine<TState, TTrigger> BuildStateMachine();
    
    /// <inheritdoc/>
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        StateMachine = BuildStateMachine();
        NotNull(StateMachine, nameof(StateMachine));
        await base.OnActivateAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Throws an exception if the provided object is null.
    /// </summary>
    /// <param name="obj">The object to check.</param>
    /// <param name="name">The name of the object.</param>
    private static void NotNull([NotNull] object? obj, string name)
    {
        if (obj == null)
            throw new InvalidOperationException($"{name} cannot be null");
    }
}
